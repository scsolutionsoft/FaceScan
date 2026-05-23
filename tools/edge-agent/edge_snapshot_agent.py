#!/usr/bin/env python3
"""
Edge Snapshot Agent

Runs on a machine inside local network near IP camera, fetches snapshot image,
and posts frames to FaceScan VPS using existing /Scan/Preview and /Scan/Verify APIs.
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from io import BytesIO
from typing import Optional

import requests
from requests.auth import HTTPBasicAuth
from requests.exceptions import RequestException


@dataclass
class AgentConfig:
    server_base_url: str
    station_code: str
    station_token: str
    snapshot_url: str
    camera_username: Optional[str]
    camera_password: Optional[str]
    interval_ms: int
    min_confidence: float
    verify_every_n_frames: int
    timeout_seconds: float
    insecure_tls: bool
    preview_enabled: bool
    agent_id: str
    heartbeat_every_n_frames: int


def parse_args() -> AgentConfig:
    parser = argparse.ArgumentParser(description="FaceScan Edge Snapshot Agent")
    parser.add_argument("--server", required=True, help="VPS URL, e.g. https://scan.example.com")
    parser.add_argument("--station-code", required=True)
    parser.add_argument("--station-token", required=True)
    parser.add_argument("--snapshot-url", required=True, help="Local camera snapshot URL")
    parser.add_argument("--camera-username", default=None)
    parser.add_argument("--camera-password", default=None)
    parser.add_argument("--interval-ms", type=int, default=1500)
    parser.add_argument("--min-confidence", type=float, default=0.60)
    parser.add_argument("--verify-every-n-frames", type=int, default=1)
    parser.add_argument("--timeout-seconds", type=float, default=8.0)
    parser.add_argument("--insecure-tls", action="store_true", help="Skip TLS certificate validation")
    parser.add_argument("--disable-preview", action="store_true", help="Skip /Scan/Preview and call /Scan/Verify directly")
    parser.add_argument("--agent-id", default="", help="Unique edge agent id")
    parser.add_argument("--heartbeat-every-n-frames", type=int, default=10)

    args = parser.parse_args()

    if args.interval_ms < 500:
        parser.error("--interval-ms must be >= 500")
    if args.verify_every_n_frames < 1:
        parser.error("--verify-every-n-frames must be >= 1")
    if args.min_confidence < 0 or args.min_confidence > 1:
        parser.error("--min-confidence must be in range [0, 1]")
    if args.heartbeat_every_n_frames < 1:
        parser.error("--heartbeat-every-n-frames must be >= 1")

    agent_id = args.agent_id.strip() if isinstance(args.agent_id, str) else ""
    if not agent_id:
        agent_id = f"{station_safe(args.station_code)}-agent"

    return AgentConfig(
        server_base_url=args.server.rstrip("/"),
        station_code=args.station_code,
        station_token=args.station_token,
        snapshot_url=args.snapshot_url,
        camera_username=args.camera_username,
        camera_password=args.camera_password,
        interval_ms=args.interval_ms,
        min_confidence=args.min_confidence,
        verify_every_n_frames=args.verify_every_n_frames,
        timeout_seconds=args.timeout_seconds,
        insecure_tls=args.insecure_tls,
        preview_enabled=not args.disable_preview,
        agent_id=agent_id,
        heartbeat_every_n_frames=args.heartbeat_every_n_frames,
    )


def station_safe(value: str) -> str:
    return "".join(ch for ch in value.strip().lower() if ch.isalnum() or ch in ("-", "_")) or "station"


def build_auth(cfg: AgentConfig) -> Optional[HTTPBasicAuth]:
    if cfg.camera_username:
        return HTTPBasicAuth(cfg.camera_username, cfg.camera_password or "")
    return None


def fetch_snapshot(session: requests.Session, cfg: AgentConfig) -> Optional[bytes]:
    try:
        resp = session.get(
            cfg.snapshot_url,
            auth=build_auth(cfg),
            timeout=cfg.timeout_seconds,
            verify=not cfg.insecure_tls,
        )
        resp.raise_for_status()
        content_type = resp.headers.get("Content-Type", "")
        if "image" not in content_type.lower():
            logging.warning("Snapshot response is not an image: %s", content_type)
        return resp.content
    except RequestException as ex:
        logging.warning("Fetch snapshot failed: %s", ex)
        return None


def make_scan_form(cfg: AgentConfig, image_bytes: bytes, filename: str = "frame.jpg"):
    files = {
        "Image": (filename, BytesIO(image_bytes), "image/jpeg"),
    }
    now_local_iso = datetime.now(timezone.utc).astimezone().isoformat()
    data = {
        "StationCode": cfg.station_code,
        "StationToken": cfg.station_token,
        "RecognitionProfile": "auto",
        "ClientCapturedAtLocal": now_local_iso,
    }
    return data, files


def call_preview(session: requests.Session, cfg: AgentConfig, image_bytes: bytes) -> Optional[dict]:
    data, files = make_scan_form(cfg, image_bytes)
    try:
        resp = session.post(
            f"{cfg.server_base_url}/Scan/Preview",
            data=data,
            files=files,
            timeout=cfg.timeout_seconds,
            verify=not cfg.insecure_tls,
        )
        if resp.status_code >= 400:
            logging.warning("Preview HTTP %s: %s", resp.status_code, resp.text[:200])
            return None
        return resp.json()
    except (RequestException, ValueError) as ex:
        logging.warning("Preview request failed: %s", ex)
        return None


def call_verify(session: requests.Session, cfg: AgentConfig, image_bytes: bytes) -> Optional[dict]:
    data, files = make_scan_form(cfg, image_bytes)
    try:
        resp = session.post(
            f"{cfg.server_base_url}/Scan/Verify",
            data=data,
            files=files,
            timeout=cfg.timeout_seconds,
            verify=not cfg.insecure_tls,
        )
        if resp.status_code >= 400:
            logging.warning("Verify HTTP %s: %s", resp.status_code, resp.text[:200])
            return None
        return resp.json()
    except (RequestException, ValueError) as ex:
        logging.warning("Verify request failed: %s", ex)
        return None


def send_heartbeat(session: requests.Session, cfg: AgentConfig, message: str) -> Optional[dict]:
    try:
        payload = {
            "StationCode": cfg.station_code,
            "StationToken": cfg.station_token,
            "AgentId": cfg.agent_id,
            "Message": message,
            "CapturedAtUtc": datetime.now(timezone.utc).isoformat(),
        }
        resp = session.post(
            f"{cfg.server_base_url}/EdgeAgent/Heartbeat",
            data=payload,
            timeout=cfg.timeout_seconds,
            verify=not cfg.insecure_tls,
        )
        if resp.status_code >= 400:
            logging.warning("Heartbeat HTTP %s: %s", resp.status_code, resp.text[:200])
            return None
        return resp.json()
    except (RequestException, ValueError) as ex:
        logging.warning("Heartbeat failed: %s", ex)
        return None


def should_verify(preview_payload: Optional[dict], cfg: AgentConfig) -> bool:
    if preview_payload is None:
        return False
    if not bool(preview_payload.get("success", False)):
        return False

    confidence = preview_payload.get("confidence")
    try:
        return float(confidence) >= cfg.min_confidence
    except (TypeError, ValueError):
        return False


def run_agent(cfg: AgentConfig) -> int:
    logging.info("Starting Edge Snapshot Agent for station %s", cfg.station_code)
    logging.info("Target server: %s", cfg.server_base_url)

    session = requests.Session()
    frame_index = 0

    while True:
        frame_started = time.time()
        frame_index += 1

        image = fetch_snapshot(session, cfg)
        if not image:
            sleep_until_next_tick(frame_started, cfg.interval_ms)
            continue

        if frame_index % cfg.heartbeat_every_n_frames == 0:
            send_heartbeat(session, cfg, "alive")

        if cfg.preview_enabled and frame_index % cfg.verify_every_n_frames != 0:
            preview = call_preview(session, cfg, image)
            if should_verify(preview, cfg):
                verify = call_verify(session, cfg, image)
                log_verify_result(verify)
            else:
                log_preview_result(preview)
        else:
            verify = call_verify(session, cfg, image)
            log_verify_result(verify)

        sleep_until_next_tick(frame_started, cfg.interval_ms)


def sleep_until_next_tick(started_epoch: float, interval_ms: int) -> None:
    elapsed_ms = int((time.time() - started_epoch) * 1000)
    wait_ms = max(interval_ms - elapsed_ms, 0)
    if wait_ms > 0:
        time.sleep(wait_ms / 1000)


def log_preview_result(payload: Optional[dict]) -> None:
    if payload is None:
        return
    if payload.get("success"):
        logging.info(
            "Preview match candidate: student=%s confidence=%s provider=%s",
            payload.get("studentCode") or payload.get("studentId") or "-",
            payload.get("confidence"),
            payload.get("provider") or "-",
        )
    else:
        logging.debug("Preview no-match: %s", payload.get("message") or "-")


def log_verify_result(payload: Optional[dict]) -> None:
    if payload is None:
        return
    if payload.get("success"):
        logging.info(
            "Verify success: student=%s name=%s confidence=%s provider=%s",
            payload.get("studentCode") or "-",
            payload.get("studentName") or "-",
            payload.get("confidence"),
            payload.get("provider") or "-",
        )
    else:
        logging.info("Verify result: success=false message=%s", payload.get("message") or "-")


def configure_logging() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )


def main() -> int:
    configure_logging()
    cfg = parse_args()
    try:
        return run_agent(cfg)
    except KeyboardInterrupt:
        logging.info("Agent stopped by user")
        return 0
    except Exception as ex:  # pylint: disable=broad-exception-caught
        logging.error("Agent crashed: %s", ex)
        return 1


if __name__ == "__main__":
    sys.exit(main())

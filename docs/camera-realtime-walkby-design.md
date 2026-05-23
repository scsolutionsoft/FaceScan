# Camera Realtime Walk-By Design

## Goals
- Connect IP camera reliably (Snapshot, RTSP, ONVIF-RTSP).
- Show live camera image on scan page in near real time.
- Feed frames into face detection and attendance verification pipeline.
- Provide dedicated walk-by page route and explicit connection test endpoint.

## Runtime Flow
1. User opens `GET /IpCamera/WalkBy?stationCode=...`.
2. Controller loads station and redirects to secure scan page with station token.
3. Browser polls `GET /IpCamera/ProxySnapshot?...` at configured interval.
4. Server chooses best frame source:
   - Preferred for `rtsp` / `onvif-rtsp`: RTSP stream (OpenCvSharp).
   - Fallback: Snapshot URL if available.
   - Fallback: RTSP candidate paths (common vendor patterns).
   - Fallback (ONVIF types): auto-recover snapshot URI via ONVIF.
5. Browser sends frame to:
   - `POST /Scan/Preview` (face gate check)
   - `POST /Scan/Verify` (attendance write)

## Key Endpoints
- `GET /IpCamera/Menu` : station launch menu.
- `GET /IpCamera/WalkBy` : dedicated walk-by entry route.
- `GET /IpCamera/ProxySnapshot` : returns JPEG frame for live scan loop.
- `GET /IpCamera/StationHealth` : station online/offline status.
- `GET /IpCamera/TestConnection` : explicit connectivity test with source diagnostics.

## Connection Test Criteria
Test is `PASS` when at least one source returns a decodable image frame:
- RTSP frame decode success (primary for RTSP types), or
- Snapshot endpoint returns image bytes.

## Operational Notes
- For RTSP devices, snapshot URL can be empty; system still works via RTSP.
- Common RTSP fallback paths are attempted automatically if configured path fails.
- If ONVIF snapshot path is stale, system attempts ONVIF recovery and persists recovered URL.

## Quick Verification
1. Save station in Devices with `CameraType=rtsp` and RTSP URL.
2. Run `GET /IpCamera/TestConnection?stationCode=...&stationToken=...`.
3. Expect JSON: `success=true` and `source=rtsp` (or `snapshot`).
4. Open `GET /IpCamera/WalkBy?stationCode=...` and confirm live frame + scan counters moving.

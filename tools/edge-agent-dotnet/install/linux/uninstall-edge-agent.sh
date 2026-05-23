#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="facescan-edge-agent"

sudo systemctl stop "$SERVICE_NAME" || true
sudo systemctl disable "$SERVICE_NAME" || true
sudo rm -f "/etc/systemd/system/${SERVICE_NAME}.service"
sudo systemctl daemon-reload

echo "Uninstalled service: $SERVICE_NAME"

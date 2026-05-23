#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="facescan-edge-agent"
APP_DIR="/opt/facescan-edge-agent"
ENV_DIR="/etc/facescan-edge-agent"

if ! id facescan >/dev/null 2>&1; then
  sudo useradd --system --no-create-home --shell /usr/sbin/nologin facescan
fi

sudo mkdir -p "$APP_DIR" "$ENV_DIR"

if [[ -f "./dist/linux-x64/FaceScan.EdgeAgent" ]]; then
  sudo cp ./dist/linux-x64/FaceScan.EdgeAgent "$APP_DIR/FaceScan.EdgeAgent"
else
  echo "Missing binary ./dist/linux-x64/FaceScan.EdgeAgent"
  exit 1
fi

if [[ ! -f "$ENV_DIR/edge-agent.env" ]]; then
  sudo cp ../FaceScan.EdgeAgent/edge-agent.sample.env "$ENV_DIR/edge-agent.env"
fi

sudo chmod +x "$APP_DIR/FaceScan.EdgeAgent"
sudo chown -R facescan:facescan "$APP_DIR"
sudo chown root:root "$ENV_DIR/edge-agent.env"
sudo chmod 600 "$ENV_DIR/edge-agent.env"

sudo cp ./install/linux/facescan-edge-agent.service.template "/etc/systemd/system/${SERVICE_NAME}.service"
sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE_NAME"
sudo systemctl restart "$SERVICE_NAME"

echo "Installed service: $SERVICE_NAME"
echo "Edit env file: $ENV_DIR/edge-agent.env"

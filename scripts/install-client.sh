#!/bin/bash
set -e

INSTALL_DIR="/opt/parental-control"
SERVICE_NAME="parental-control-client"
LOG_DIR="/var/log/parental-control"
DATA_DIR="/var/lib/parental-control"

if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root"
    exit 1
fi

echo "Creating directories..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$LOG_DIR"
mkdir -p "$DATA_DIR"

echo "Copying files..."
cp -r ../src/ParentalControl.Client/bin/Release/net10.0/linux-x64/publish/* "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/ParentalControl.Client"

echo "Installing systemd service..."
cp parental-control-client.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

echo "Starting service..."
systemctl start "$SERVICE_NAME"

echo "Installation complete!"
systemctl status "$SERVICE_NAME"

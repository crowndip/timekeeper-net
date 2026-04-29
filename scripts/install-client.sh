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
mkdir -p /etc/parental-control

echo "Creating parental-control group for tray app access..."
groupadd -f parental-control
# Add all regular users (UID >= 1000) to the group so the tray app can read config
while IFS=: read -r username _ uid _; do
    if [ "$uid" -ge 1000 ] && [ "$uid" -lt 65534 ]; then
        usermod -aG parental-control "$username" 2>/dev/null || true
        echo "  Added $username to parental-control group"
    fi
done < /etc/passwd

# Set directory ownership so group members can read files inside
chown root:parental-control /etc/parental-control
chmod 750 /etc/parental-control

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
echo "Note: Users must log out and back in for group membership to take effect."
systemctl status "$SERVICE_NAME"

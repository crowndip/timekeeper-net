#!/bin/bash
set -e

# Parental Control Client - Automatic Installer
# Usage: curl -fsSL https://raw.githubusercontent.com/crowndip/timekeeper-net/main/scripts/install-linux-client.sh | sudo bash -s -- <SERVER_URL>

INSTALL_DIR="/opt/parental-control"
SERVICE_NAME="parental-control-client"
GITHUB_REPO="crowndip/timekeeper-net"
LATEST_RELEASE="v1.4.0"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}Parental Control Client - Automatic Installer${NC}"
echo "================================================"

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}Error: Please run as root (use sudo)${NC}"
    exit 1
fi

# Get server URL from argument
SERVER_URL="${1:-}"
if [ -z "$SERVER_URL" ]; then
    echo -e "${YELLOW}Enter server URL (e.g., http://192.168.1.100:8080):${NC}"
    read -r SERVER_URL
fi

if [ -z "$SERVER_URL" ]; then
    echo -e "${RED}Error: Server URL is required${NC}"
    exit 1
fi

echo "Server URL: $SERVER_URL"
echo ""

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)
        ARCH="x64"
        ;;
    aarch64)
        ARCH="arm64"
        ;;
    *)
        echo -e "${RED}Error: Unsupported architecture: $ARCH${NC}"
        exit 1
        ;;
esac

echo "Detected architecture: $ARCH"

# Download URL
DOWNLOAD_URL="https://github.com/${GITHUB_REPO}/releases/download/${LATEST_RELEASE}/client-linux-${ARCH}.tar.gz"

echo "Downloading client from: $DOWNLOAD_URL"

# Create temporary directory
TMP_DIR=$(mktemp -d)
cd "$TMP_DIR"

# Download client
echo "Downloading..."
if ! curl -fsSL -o client.tar.gz "$DOWNLOAD_URL"; then
    echo -e "${RED}Error: Failed to download client${NC}"
    echo "URL: $DOWNLOAD_URL"
    rm -rf "$TMP_DIR"
    exit 1
fi

# Extract
echo "Extracting..."
tar -xzf client.tar.gz

# Stop existing service if running
if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo "Stopping existing service..."
    systemctl stop "$SERVICE_NAME"
fi

# Create installation directory
echo "Installing to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"

# Copy files
cp -r client-linux-${ARCH}/* "$INSTALL_DIR/"

# Create appsettings.json
cat > "$INSTALL_DIR/appsettings.json" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ParentalControl": {
    "ServerUrl": "$SERVER_URL",
    "TickIntervalSeconds": 60
  }
}
EOF

# Create systemd service
cat > /etc/systemd/system/${SERVICE_NAME}.service << EOF
[Unit]
Description=Parental Control Client
After=network.target

[Service]
Type=notify
ExecStart=$INSTALL_DIR/ParentalControl.Client
WorkingDirectory=$INSTALL_DIR
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=parental-control-client
User=root

[Install]
WantedBy=multi-user.target
EOF

# Set permissions
chmod +x "$INSTALL_DIR/ParentalControl.Client"
chmod 644 "$INSTALL_DIR/appsettings.json"

# Reload systemd
echo "Configuring systemd service..."
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

# Start service
echo "Starting service..."
systemctl start "$SERVICE_NAME"

# Cleanup
cd /
rm -rf "$TMP_DIR"

# Check status
sleep 2
if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo ""
    echo -e "${GREEN}✅ Installation successful!${NC}"
    echo ""
    echo "Service status:"
    systemctl status "$SERVICE_NAME" --no-pager -l
    echo ""
    echo "Configuration file: $INSTALL_DIR/appsettings.json"
    echo "Logs: journalctl -u $SERVICE_NAME -f"
    echo ""
    echo "To uninstall:"
    echo "  sudo systemctl stop $SERVICE_NAME"
    echo "  sudo systemctl disable $SERVICE_NAME"
    echo "  sudo rm -rf $INSTALL_DIR"
    echo "  sudo rm /etc/systemd/system/${SERVICE_NAME}.service"
else
    echo ""
    echo -e "${RED}⚠️  Service failed to start${NC}"
    echo "Check logs: journalctl -u $SERVICE_NAME -n 50"
    exit 1
fi

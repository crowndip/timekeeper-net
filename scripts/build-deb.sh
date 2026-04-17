#!/bin/bash
set -e

# Build .deb package for Parental Control Client
# Usage: ./build-deb.sh [version]

VERSION="${1:-1.4.1}"
ARCH="amd64"
PACKAGE_NAME="parental-control-client"
BUILD_DIR="build/deb"
PACKAGE_DIR="${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}"

echo "Building ${PACKAGE_NAME} ${VERSION} for ${ARCH}"

# Clean previous build
rm -rf "$BUILD_DIR"
mkdir -p "$PACKAGE_DIR"

# Build the client
echo "Building client..."
cd ../src/ParentalControl.Client
dotnet publish -c Release -r linux-x64 --self-contained -o ../../scripts/build/publish
cd ../../scripts

# Create package structure
mkdir -p "${PACKAGE_DIR}/opt/parental-control"
mkdir -p "${PACKAGE_DIR}/etc/systemd/system"
mkdir -p "${PACKAGE_DIR}/DEBIAN"

# Copy application files
cp -r build/publish/* "${PACKAGE_DIR}/opt/parental-control/"

# Create default config
cat > "${PACKAGE_DIR}/opt/parental-control/appsettings.json" << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ParentalControl": {
    "ServerUrl": "http://localhost:8080",
    "TickIntervalSeconds": 60
  }
}
EOF

# Create systemd service
cat > "${PACKAGE_DIR}/etc/systemd/system/parental-control-client.service" << 'EOF'
[Unit]
Description=Parental Control Client
After=network.target

[Service]
Type=notify
ExecStart=/opt/parental-control/ParentalControl.Client
WorkingDirectory=/opt/parental-control
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=parental-control-client
User=root

[Install]
WantedBy=multi-user.target
EOF

# Create control file
cat > "${PACKAGE_DIR}/DEBIAN/control" << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: Parental Control <support@example.com>
Description: Parental Control Client
 Client agent for the Parental Control System.
 Monitors user sessions and enforces time limits.
Homepage: https://github.com/crowndip/timekeeper-net
EOF

# Create postinst script
cat > "${PACKAGE_DIR}/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# Reload systemd
systemctl daemon-reload

# Enable service
systemctl enable parental-control-client.service

echo ""
echo "Parental Control Client installed successfully!"
echo ""
echo "Next steps:"
echo "1. Edit /opt/parental-control/appsettings.json"
echo "2. Set ServerUrl to your server address"
echo "3. Start service: sudo systemctl start parental-control-client"
echo "4. Check status: sudo systemctl status parental-control-client"
echo ""

exit 0
EOF

# Create prerm script
cat > "${PACKAGE_DIR}/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Stop service if running
if systemctl is-active --quiet parental-control-client; then
    systemctl stop parental-control-client
fi

# Disable service
systemctl disable parental-control-client.service || true

exit 0
EOF

# Create postrm script
cat > "${PACKAGE_DIR}/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e

# Reload systemd
systemctl daemon-reload

exit 0
EOF

# Set permissions
chmod 755 "${PACKAGE_DIR}/DEBIAN/postinst"
chmod 755 "${PACKAGE_DIR}/DEBIAN/prerm"
chmod 755 "${PACKAGE_DIR}/DEBIAN/postrm"
chmod 755 "${PACKAGE_DIR}/opt/parental-control/ParentalControl.Client"

# Build package
echo "Building .deb package..."
dpkg-deb --build "$PACKAGE_DIR"

# Move to output
mv "${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb" .

# Cleanup
rm -rf "$BUILD_DIR"

echo ""
echo "✅ Package built successfully!"
echo "File: ${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
echo ""
echo "Install with:"
echo "  sudo dpkg -i ${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
echo ""
echo "Or upload to GitHub releases"

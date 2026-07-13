#!/bin/bash
set -e

VERSION="${1:-1.4.1}"
ARCH="amd64"
PACKAGE_NAME="parental-control-client"
BUILD_DIR="../build/client-deb"
PACKAGE_DIR="${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}"

echo "Building ${PACKAGE_NAME} ${VERSION} for ${ARCH}"

# Clean previous build
rm -rf "$BUILD_DIR"
mkdir -p "$PACKAGE_DIR"

# Build the client
echo "Building client..."
dotnet publish ../src/ParentalControl.Client/ParentalControl.Client.csproj \
    -c Release -r linux-x64 --self-contained \
    -o "${PACKAGE_DIR}/opt/parental-control"

# Create package structure
mkdir -p "${PACKAGE_DIR}/etc/systemd/system"
mkdir -p "${PACKAGE_DIR}/DEBIAN"

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

# Use the canonical, hardened systemd unit (ProtectSystem=strict + the ReadWritePaths
# needed for LocalCache's persisted cache.json and the client's api-key file) instead of
# maintaining a second copy here that silently drifts out of sync with it.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "${SCRIPT_DIR}/parental-control-client.service" "${PACKAGE_DIR}/etc/systemd/system/parental-control-client.service"

# Mark appsettings.json as a conffile: without this, dpkg silently overwrites it on
# every upgrade (files under /opt aren't conffiles by default), which would revert a
# ServerUrl the parent edited directly in the file back to the localhost placeholder.
# ServerUrl is only a fallback -- LoadServerUrl() in ServerSyncService prefers the
# persisted /etc/parental-control/server-url file, but the file is still there and
# some parents will edit it directly, so it needs upgrade-safe handling either way.
cat > "${PACKAGE_DIR}/DEBIAN/conffiles" << 'EOF'
/opt/parental-control/appsettings.json
EOF

# Create control file
cat > "${PACKAGE_DIR}/DEBIAN/control" << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Replaces: ${PACKAGE_NAME} (<< ${VERSION})
Conflicts: ${PACKAGE_NAME} (<< ${VERSION})
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

# Restart service if it was already running (upgrade scenario)
if [ "$1" = "configure" ] && [ -n "$2" ]; then
    # This is an upgrade. Note: this heredoc is single-quoted (see the 'EOF' above), so
    # ${VERSION} from the outer script is NOT interpolated here and would print empty at
    # runtime -- only $2 (the previous version, passed by dpkg to postinst itself) is
    # meant to be resolved at install time, not by this script.
    echo "Upgrading from version $2..."
    systemctl restart parental-control-client.service || true
else
    # This is a fresh install
    echo ""
    echo "Parental Control Client installed successfully!"
    echo ""
    echo "Next steps:"
    echo "1. Set the server address:"
    echo "     sudo /opt/parental-control/ParentalControl.Client set server-url http://your-server:8080"
    echo "   (this persists to /etc/parental-control/server-url and survives package upgrades;"
    echo "    editing appsettings.json directly also works but is only used as a fallback)"
    echo "2. Start service: sudo systemctl start parental-control-client"
    echo "3. Check status: sudo systemctl status parental-control-client"
    echo ""
fi

exit 0
EOF

# Create prerm script
cat > "${PACKAGE_DIR}/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Only stop/disable on removal, not on upgrade
if [ "$1" = "remove" ]; then
    # Stop service if running
    if systemctl is-active --quiet parental-control-client; then
        systemctl stop parental-control-client
    fi

    # Disable service
    systemctl disable parental-control-client.service || true
fi

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

echo ""
echo "✅ Package built successfully!"
echo "File: ${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"

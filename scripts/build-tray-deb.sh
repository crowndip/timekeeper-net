#!/bin/bash
set -e

VERSION="$1"
if [ -z "$VERSION" ]; then
    echo "Usage: $0 <version>"
    exit 1
fi

ARCH="amd64"
PACKAGE_NAME="parental-control-tray"
BUILD_DIR="../build/tray-deb"
PACKAGE_DIR="${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}"

# Clean and create directories
rm -rf "${BUILD_DIR}"
mkdir -p "${PACKAGE_DIR}/DEBIAN"
mkdir -p "${PACKAGE_DIR}/usr/local/bin"
mkdir -p "${PACKAGE_DIR}/etc/xdg/autostart"

# Build the tray application
echo "Building tray application..."
dotnet publish ../src/ParentalControl.TrayIcon/ParentalControl.TrayIcon.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o "${PACKAGE_DIR}/usr/local/bin/parental-control-tray"

# Create control file
cat > "${PACKAGE_DIR}/DEBIAN/control" << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Depends: libx11-6, libice6, libsm6
Maintainer: Parental Control <support@example.com>
Description: Parental Control Tray Icon
 Shows remaining time in system tray for the current user.
 Lightweight application that displays time limits.
Homepage: https://github.com/crowndip/timekeeper-net
EOF

# Create desktop autostart file
cat > "${PACKAGE_DIR}/etc/xdg/autostart/parental-control-tray.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Parental Control Tray
Comment=Show remaining time in system tray
Exec=/usr/local/bin/parental-control-tray/ParentalControl.TrayIcon
Terminal=false
Hidden=false
X-GNOME-Autostart-enabled=true
EOF

# Build package
echo "Building .deb package..."
dpkg-deb --build "${PACKAGE_DIR}"

echo "Package created: ${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"

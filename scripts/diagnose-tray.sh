#!/bin/bash

echo "=== Parental Control Tray Icon Diagnostics ==="
echo ""

echo "1. Checking installation..."
if [ -d "/usr/local/bin/parental-control-tray" ]; then
    echo "✓ Tray app installed at /usr/local/bin/parental-control-tray"
    ls -lh /usr/local/bin/parental-control-tray/ParentalControl.TrayIcon
else
    echo "✗ Tray app NOT installed"
    exit 1
fi

echo ""
echo "2. Checking autostart file..."
if [ -f "/etc/xdg/autostart/parental-control-tray.desktop" ]; then
    echo "✓ Autostart file exists"
    cat /etc/xdg/autostart/parental-control-tray.desktop
else
    echo "✗ Autostart file NOT found"
fi

echo ""
echo "3. Checking if tray app is running..."
if pgrep -f "ParentalControl.TrayIcon" > /dev/null; then
    echo "✓ Tray app is running"
    ps aux | grep ParentalControl.TrayIcon | grep -v grep
else
    echo "✗ Tray app is NOT running"
fi

echo ""
echo "4. Checking configuration files..."
if [ -f "/etc/parental-control/server-url" ]; then
    echo "✓ Server URL configured:"
    cat /etc/parental-control/server-url
else
    echo "✗ Server URL NOT configured"
fi

if [ -f "/etc/parental-control/computer-id" ]; then
    echo "✓ Computer ID configured"
else
    echo "✗ Computer ID NOT configured"
fi

echo ""
echo "5. Checking desktop environment..."
echo "Desktop: $XDG_CURRENT_DESKTOP"
echo "Session: $DESKTOP_SESSION"

echo ""
echo "6. Testing tray app manually..."
echo "Running: /usr/local/bin/parental-control-tray/ParentalControl.TrayIcon"
echo "Press Ctrl+C to stop after a few seconds..."
echo ""
/usr/local/bin/parental-control-tray/ParentalControl.TrayIcon

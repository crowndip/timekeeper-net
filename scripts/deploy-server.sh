#!/bin/bash
set -e

# Parental Control Server - Automated Deployment Script
# This script downloads and deploys the latest server image for Portainer

GITHUB_REPO="crowndip/timekeeper-net"
IMAGE_NAME="parental-control-webservice"
DOWNLOAD_DIR="/tmp/parental-control-deploy"

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║     Parental Control Server - Automated Deployment                  ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""

# Check if running as root or with sudo
if [ "$EUID" -ne 0 ]; then 
    echo "⚠️  This script requires sudo privileges for Docker operations."
    echo "Please run with: sudo $0"
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

# Get latest release version
echo "📡 Fetching latest release version..."
LATEST_VERSION=$(curl -s "https://api.github.com/repos/$GITHUB_REPO/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')

if [ -z "$LATEST_VERSION" ]; then
    echo "❌ Failed to fetch latest version from GitHub."
    exit 1
fi

echo "✅ Latest version: $LATEST_VERSION"
echo ""

# Create download directory
mkdir -p "$DOWNLOAD_DIR"
cd "$DOWNLOAD_DIR"

# Download image
DOWNLOAD_URL="https://github.com/$GITHUB_REPO/releases/download/$LATEST_VERSION/webservice-image.tar.gz"
echo "📥 Downloading webservice-image.tar.gz..."
echo "   From: $DOWNLOAD_URL"

if ! curl -L -o webservice-image.tar.gz "$DOWNLOAD_URL"; then
    echo "❌ Failed to download image."
    exit 1
fi

echo "✅ Download complete"
echo ""

# Extract
echo "📦 Extracting archive..."
tar -xzf webservice-image.tar.gz

if [ ! -f "webservice-image.tar" ]; then
    echo "❌ webservice-image.tar not found in archive."
    exit 1
fi

echo "✅ Extracted successfully"
echo ""

# Load Docker image
echo "🐳 Loading Docker image..."
LOAD_OUTPUT=$(docker load -i webservice-image.tar)
echo "$LOAD_OUTPUT"

# Extract image hash from output
IMAGE_HASH=$(echo "$LOAD_OUTPUT" | grep -oP 'parental-control-webservice:\K[a-f0-9]+')

if [ -z "$IMAGE_HASH" ]; then
    echo "❌ Failed to extract image hash from docker load output."
    exit 1
fi

echo "✅ Image loaded with hash: $IMAGE_HASH"
echo ""

# Check if 'latest' tag already exists
if docker images "$IMAGE_NAME:latest" --format "{{.Repository}}:{{.Tag}}" | grep -q "latest"; then
    echo "⚠️  Image '$IMAGE_NAME:latest' already exists."
    read -p "   Do you want to replace it? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "❌ Deployment cancelled."
        exit 1
    fi
fi

# Tag as latest
echo "🏷️  Tagging image as 'latest'..."
docker tag "$IMAGE_NAME:$IMAGE_HASH" "$IMAGE_NAME:latest"
echo "✅ Tagged successfully"
echo ""

# Verify
echo "🔍 Verifying Docker images..."
docker images | grep "$IMAGE_NAME"
echo ""

# Cleanup
echo "🧹 Cleaning up temporary files..."
cd /
rm -rf "$DOWNLOAD_DIR"
echo "✅ Cleanup complete"
echo ""

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║                    ✅ DEPLOYMENT SUCCESSFUL! ✅                      ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""
echo "📋 NEXT STEPS:"
echo ""
echo "1. Go to Portainer: http://your-portainer-url:9000"
echo "2. Navigate to: Stacks → parental-control"
echo "3. Click 'Update the stack'"
echo "4. Click 'Update' (no changes needed)"
echo "5. Portainer will pull the new 'latest' image and restart"
echo ""
echo "🌐 Access your server at: http://localhost:8080"
echo ""
echo "📚 First time setup?"
echo "   - Copy docker-compose.yml from GitHub release page"
echo "   - Create new stack in Portainer"
echo "   - Set DB_PASSWORD environment variable"
echo "   - Deploy stack"
echo "   - Visit http://localhost:8080/setup to initialize database"
echo ""

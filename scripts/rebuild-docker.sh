#!/bin/bash
set -e

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║     Parental Control Server - Docker Compose Rebuild                ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""

# Check if docker-compose.yml exists
if [ ! -f "docker-compose.yml" ]; then
    echo "❌ docker-compose.yml not found in current directory"
    echo "Please run this script from the project root"
    exit 1
fi

# Check if running as root or with sudo
if [ "$EUID" -ne 0 ]; then 
    echo "⚠️  This script requires sudo privileges for Docker operations."
    echo "Please run with: sudo $0"
    exit 1
fi

echo "📋 Current status:"
docker-compose ps
echo ""

echo "🛑 Stopping services..."
docker-compose down
echo "✅ Services stopped"
echo ""

echo "🔨 Rebuilding webservice image (no cache)..."
docker-compose build --no-cache webservice
echo "✅ Build complete"
echo ""

echo "🚀 Starting services..."
docker-compose up -d
echo "✅ Services started"
echo ""

echo "⏳ Waiting for services to be ready..."
sleep 5
echo ""

echo "📋 Service status:"
docker-compose ps
echo ""

echo "📝 Recent logs:"
docker-compose logs --tail=20 webservice
echo ""

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║                    ✅ REBUILD SUCCESSFUL! ✅                         ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""
echo "🌐 Access your server at: http://localhost:8080"
echo ""
echo "📊 Check Configuration widget on dashboard to verify schema version"
echo "   Expected: 20260420 (with AddUserAliases migration)"
echo ""
echo "📝 View logs: docker-compose logs -f webservice"
echo ""

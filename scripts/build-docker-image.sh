#!/bin/bash
set -e

echo "Building Parental Control Web Service Docker Image..."

cd "$(dirname "$0")/.."

docker build \
  -f docker/Dockerfile.webservice \
  -t parental-control-webservice:latest \
  .

echo ""
echo "✅ Image built successfully: parental-control-webservice:latest"
echo ""
echo "Next steps:"
echo "1. Copy docker/init.sql to /opt/parental-control/db/init.sql"
echo "2. Deploy via Portainer using docker-compose.yml"
echo "3. Set DB_PASSWORD environment variable in Portainer"

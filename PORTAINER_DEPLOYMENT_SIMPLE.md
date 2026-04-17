# Parental Control Server - Portainer Deployment (Simplified)

This guide shows the easiest way to deploy the Parental Control Server using Portainer.

## Prerequisites

- Docker installed
- Portainer installed and running
- Access to Portainer web UI (usually http://localhost:9000)

## Quick Start (First Time)

### Step 1: Create Stack in Portainer

1. Open Portainer web UI
2. Go to **Stacks** → **Add stack**
3. Name it: `parental-control`
4. Copy `docker-compose.yml` from the [latest release](https://github.com/crowndip/timekeeper-net/releases/latest)
5. Paste it into the Web editor
6. Scroll down to **Environment variables**
7. Add variable:
   - Name: `DB_PASSWORD`
   - Value: `your_secure_password` (choose a strong password)
8. Click **Deploy the stack**

### Step 2: Load Docker Image

Download and run the automated deployment script:

```bash
# Download the script from latest release
wget https://github.com/crowndip/timekeeper-net/releases/latest/download/deploy-server.sh

# Make it executable
chmod +x deploy-server.sh

# Run with sudo
sudo ./deploy-server.sh
```

The script will:
- ✅ Download the latest Docker image
- ✅ Extract and load it into Docker
- ✅ Tag it as 'latest'
- ✅ Verify the installation
- ✅ Clean up temporary files

### Step 3: Reload Stack in Portainer

1. Go to **Stacks** → **parental-control**
2. Click **Update the stack**
3. Click **Update** (no changes needed)
4. Wait for the stack to restart

### Step 4: Initialize Database

1. Open your browser
2. Go to: `http://localhost:8080/setup`
3. Click **Initialize Database**
4. Wait for confirmation
5. You'll be redirected to the home page

**Done!** Your server is now running.

## Updating to a New Version

When a new version is released:

```bash
# Run the deployment script again
sudo ./deploy-server.sh

# Then reload the stack in Portainer:
# Stacks → parental-control → Update the stack → Update
```

That's it! The script handles everything automatically.

## Manual Deployment (Alternative)

If you prefer to do it manually:

```bash
# Download image
wget https://github.com/crowndip/timekeeper-net/releases/latest/download/webservice-image.tar.gz

# Extract
tar -xzf webservice-image.tar.gz

# Load into Docker
docker load -i webservice-image.tar
# Output: Loaded image: parental-control-webservice:abc123...

# Tag as latest (replace <hash> with the one from above)
docker tag parental-control-webservice:<hash> parental-control-webservice:latest

# Verify
docker images | grep parental-control

# Reload stack in Portainer
```

## Accessing the Server

- **Web UI:** http://localhost:8080
- **Setup Page:** http://localhost:8080/setup
- **API Docs:** http://localhost:8080/swagger

## Troubleshooting

### Stack fails to start

**Error:** "pull access denied for parental-control-webservice"

**Solution:** You need to load the Docker image first (Step 2 above). Portainer can't pull the image from a registry because it's a local image.

### Database connection errors

1. Check if PostgreSQL container is running:
   ```bash
   docker ps | grep postgres
   ```

2. Check logs:
   ```bash
   docker logs parental-control-db
   docker logs parental-control-webservice
   ```

3. Verify `DB_PASSWORD` environment variable matches in both containers

### Can't access web UI

1. Check if the service is running:
   ```bash
   docker ps | grep parental-control
   ```

2. Check port mapping:
   ```bash
   docker port parental-control-webservice
   ```

3. Try accessing: http://localhost:8080/setup

## Configuration

### Change Database Password

1. Stop the stack in Portainer
2. Edit stack → Change `DB_PASSWORD` environment variable
3. Update the stack
4. Database will restart with new password

### Change Web Port

Edit `docker-compose.yml` in Portainer:

```yaml
services:
  webservice:
    ports:
      - "9090:8080"  # Change 9090 to your desired port
```

### Enable Reverse Proxy Support

If accessing from external network with Basic Authentication:

1. Edit stack in Portainer
2. Add environment variables:
   ```yaml
   environment:
     - ReverseProxy__Enabled=true
     - ReverseProxy__Username=client-user
     - ReverseProxy__Password=secure-password
   ```
3. Update the stack

See [REVERSE_PROXY.md](REVERSE_PROXY.md) for complete reverse proxy setup.

## Next Steps

- Install Linux client: See [README.md](README.md#linux-client)
- Install Windows client: See [WINDOWS_CLIENT.md](WINDOWS_CLIENT.md)
- Configure users and time limits in the web UI
- Set up reverse proxy for external access: [REVERSE_PROXY.md](REVERSE_PROXY.md)

## Support

- **Documentation:** [README.md](README.md)
- **Detailed Guide:** [PORTAINER_DEPLOYMENT_DETAILED.md](PORTAINER_DEPLOYMENT_DETAILED.md)
- **Error Scenarios:** [ERROR_SCENARIOS.md](ERROR_SCENARIOS.md)
- **Issues:** https://github.com/crowndip/timekeeper-net/issues

# Detailed Portainer Deployment Guide

## Overview

The error occurs because Portainer is trying to pull the image from Docker Hub, but the image only exists locally. You need to load the image first, then deploy the stack.

## Prerequisites

- Portainer installed and running
- Docker installed on the server
- Access to the server via SSH or terminal
- Downloaded `server-deployment.tar.gz` from GitHub releases

## Step-by-Step Deployment

### Step 1: Download the Release

Download the latest release from GitHub:

```bash
# Download the server deployment package
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.1.0/server-deployment.tar.gz

# Or use curl
curl -L -o server-deployment.tar.gz https://github.com/crowndip/timekeeper-net/releases/download/v1.1.0/server-deployment.tar.gz
```

### Step 2: Extract the Package

```bash
# Create a directory for the deployment
mkdir -p ~/parental-control-server
cd ~/parental-control-server

# Extract the package
tar -xzf ~/server-deployment.tar.gz

# Verify contents
ls -la
# You should see:
# - webservice-image.tar
# - docker-compose.yml
# - init.sql
# - README.md
# - PORTAINER_DEPLOYMENT.md
```

### Step 3: Load the Docker Image

**CRITICAL STEP:** Load the Docker image into Docker before deploying in Portainer.

```bash
# Load the image
docker load -i webservice-image.tar

# Verify the image is loaded
docker images | grep parental-control

# You should see something like:
# parental-control-webservice   <commit-hash>   ...   ...   ...
```

**Note the image tag** (the commit hash). You'll need this for the docker-compose.yml.

### Step 4: Tag the Image (Important!)

The docker-compose.yml expects the image to be tagged as `latest`:

```bash
# Get the image ID or tag
docker images | grep parental-control-webservice

# Tag it as latest (replace <commit-hash> with actual hash)
docker tag parental-control-webservice:<commit-hash> parental-control-webservice:latest

# Verify
docker images | grep parental-control-webservice
# You should now see both tags
```

### Step 5: Prepare the Database Initialization Script

```bash
# Create directory for database initialization
sudo mkdir -p /opt/parental-control/db

# Copy the init script
sudo cp init.sql /opt/parental-control/db/init.sql

# Set permissions
sudo chmod 644 /opt/parental-control/db/init.sql
```

### Step 6: Update docker-compose.yml

Edit the `docker-compose.yml` to ensure it uses the correct image tag:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: parental-control-db
    environment:
      POSTGRES_DB: parental_control
      POSTGRES_USER: parental_control_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - /opt/parental-control/db/init.sql:/docker-entrypoint-initdb.d/init.sql:ro
    networks:
      - parental-control-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U parental_control_user -d parental_control"]
      interval: 10s
      timeout: 5s
      retries: 5

  webservice:
    image: parental-control-webservice:latest  # Make sure this matches your tag
    container_name: parental-control-webservice
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=parental_control;Username=parental_control_user;Password=${DB_PASSWORD}"
      DbPassword: ${DB_PASSWORD}
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "8080:8080"
    networks:
      - parental-control-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

networks:
  parental-control-network:
    driver: bridge

volumes:
  postgres_data:
```

### Step 7: Deploy in Portainer

#### Option A: Using Portainer Web UI

1. **Open Portainer** in your browser (usually http://your-server:9000)

2. **Navigate to Stacks**
   - Click on "Stacks" in the left sidebar
   - Click "Add stack" button

3. **Create the Stack**
   - **Name**: `parental-control`
   - **Build method**: Select "Web editor"
   - **Web editor**: Paste the contents of `docker-compose.yml`

4. **Set Environment Variables**
   - Scroll down to "Environment variables"
   - Click "Add environment variable"
   - **Name**: `DB_PASSWORD`
   - **Value**: Choose a strong password (e.g., `MySecurePassword123!`)

5. **Deploy the Stack**
   - Click "Deploy the stack" button
   - Wait for deployment to complete

6. **Verify Deployment**
   - Go to "Containers" in Portainer
   - You should see two containers:
     - `parental-control-db` (running)
     - `parental-control-webservice` (running)

#### Option B: Using Docker Compose CLI

If you prefer command line:

```bash
# Set the database password
export DB_PASSWORD="MySecurePassword123!"

# Deploy the stack
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f
```

### Step 8: Verify the Deployment

1. **Check Container Status**
   ```bash
   docker ps | grep parental-control
   ```
   Both containers should be "Up" and healthy.

2. **Check Logs**
   ```bash
   # Database logs
   docker logs parental-control-db

   # Web service logs
   docker logs parental-control-webservice
   ```

3. **Test the Web Interface**
   ```bash
   # Test health endpoint
   curl http://localhost:8080/health

   # Should return: Healthy
   ```

4. **Access the Admin UI**
   - Open browser: http://your-server-ip:8080
   - If database is not initialized, you'll be redirected to `/setup`
   - Click "Initialize Database"
   - Once initialized, you'll see the admin interface

### Step 9: Initialize the Database (if needed)

If you see the setup page:

1. Visit: http://your-server-ip:8080/setup
2. The page will check the database connection
3. If connected, click "Initialize Database"
4. Wait for initialization to complete
5. You'll be redirected to the home page

### Step 10: Create First User

After initialization:

1. Go to the admin interface
2. Navigate to "Users" section
3. Create a parent account (for yourself)
4. Create child accounts (for users to monitor)
5. Set up time profiles for each child

## Troubleshooting

### Issue 1: Image Not Found

**Error**: `pull access denied for parental-control-webservice`

**Solution**:
```bash
# Make sure the image is loaded
docker images | grep parental-control

# If not found, load it again
docker load -i webservice-image.tar

# Tag it as latest
docker tag parental-control-webservice:<hash> parental-control-webservice:latest
```

### Issue 2: Database Connection Failed

**Error**: `Cannot connect to database`

**Solution**:
```bash
# Check if PostgreSQL container is running
docker ps | grep postgres

# Check PostgreSQL logs
docker logs parental-control-db

# Verify the password is set correctly
docker exec parental-control-db psql -U parental_control_user -d parental_control -c "SELECT 1"
```

### Issue 3: Port Already in Use

**Error**: `port is already allocated`

**Solution**:
```bash
# Check what's using port 8080
sudo lsof -i :8080

# Either stop the conflicting service or change the port in docker-compose.yml
# Change "8080:8080" to "8081:8080" (or any other available port)
```

### Issue 4: Permission Denied on init.sql

**Error**: `permission denied` when mounting init.sql

**Solution**:
```bash
# Fix permissions
sudo chmod 644 /opt/parental-control/db/init.sql
sudo chown root:root /opt/parental-control/db/init.sql

# Restart the stack
docker-compose down
docker-compose up -d
```

### Issue 5: Container Keeps Restarting

**Check logs**:
```bash
docker logs parental-control-webservice --tail 100

# Common issues:
# - Database not ready: Wait 30 seconds and check again
# - Wrong connection string: Check DB_PASSWORD environment variable
# - Port conflict: Change port in docker-compose.yml
```

## Alternative: Build from Source

If you prefer to build the image yourself:

```bash
# Clone the repository
git clone https://github.com/crowndip/timekeeper-net.git
cd timekeeper-net

# Build the Docker image
docker build -f docker/Dockerfile.webservice -t parental-control-webservice:latest .

# Deploy with docker-compose
cd /path/to/deployment
docker-compose up -d
```

## Security Recommendations

1. **Change Default Password**
   - Use a strong database password
   - Don't use the example password

2. **Firewall Configuration**
   ```bash
   # Allow only necessary ports
   sudo ufw allow 8080/tcp
   sudo ufw enable
   ```

3. **Reverse Proxy (Optional)**
   - Set up nginx or Traefik for HTTPS
   - See REVERSE_PROXY.md for details

4. **Backup Database**
   ```bash
   # Backup PostgreSQL data
   docker exec parental-control-db pg_dump -U parental_control_user parental_control > backup.sql
   ```

## Quick Reference Commands

```bash
# View all containers
docker ps -a

# View logs
docker logs -f parental-control-webservice

# Restart stack
docker-compose restart

# Stop stack
docker-compose down

# Start stack
docker-compose up -d

# Remove everything (including data!)
docker-compose down -v

# Update image
docker load -i new-webservice-image.tar
docker-compose up -d
```

## Support

If you encounter issues:

1. Check logs: `docker logs parental-control-webservice`
2. Check database: `docker logs parental-control-db`
3. Visit setup page: http://your-server:8080/setup
4. Check GitHub issues: https://github.com/crowndip/timekeeper-net/issues

## Summary

**Key Steps:**
1. Download server-deployment.tar.gz
2. Extract files
3. **Load Docker image**: `docker load -i webservice-image.tar`
4. **Tag image**: `docker tag parental-control-webservice:<hash> parental-control-webservice:latest`
5. Copy init.sql to /opt/parental-control/db/
6. Deploy in Portainer with DB_PASSWORD environment variable
7. Access http://your-server:8080
8. Initialize database via /setup page
9. Create users and time profiles

**Most Common Mistake:** Forgetting to load the Docker image before deploying in Portainer!

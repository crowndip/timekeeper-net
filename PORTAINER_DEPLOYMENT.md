# Portainer Deployment Guide - Quick Start

## Complete Deployment in 6 Steps (5 Minutes)

### Prerequisites
- Server with Docker and Portainer installed
- SSH access to the server
- Downloaded `server-deployment.tar.gz` from [GitHub Releases](https://github.com/crowndip/timekeeper-net/releases)

---

## Step 1: Download and Extract

SSH into your server and run:

```bash
# Download the release (replace v1.1.0 with latest version)
cd ~
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.1.0/server-deployment.tar.gz

# Extract the files
tar -xzf server-deployment.tar.gz

# Verify contents
ls -la
# You should see:
# - webservice-image.tar (Docker image)
# - docker-compose.yml (Stack configuration)
# - init.sql (Database initialization)
# - README.md
```

---

## Step 2: Load the Docker Image ⚠️ CRITICAL

**This is the most important step!** You must load the image before deploying in Portainer.

```bash
# Load the image into Docker
docker load -i webservice-image.tar

# Output will show:
# Loaded image: parental-control-webservice:3bc9126e0f620250aa3506827ddb69477b24af69
# ↑ Note this hash, you'll need it in the next command

# Tag it as 'latest' (replace <hash> with the one from above)
docker tag parental-control-webservice:3bc9126e0f620250aa3506827ddb69477b24af69 parental-control-webservice:latest

# Verify the image is loaded
docker images | grep parental-control

# You should see TWO entries with the same IMAGE ID:
# parental-control-webservice   3bc9126...   ...
# parental-control-webservice   latest       ...
```

---

## Step 3: Prepare Database Initialization

```bash
# Create directory for database init script
sudo mkdir -p /opt/parental-control/db

# Copy the init script
sudo cp init.sql /opt/parental-control/db/init.sql

# Set correct permissions
sudo chmod 644 /opt/parental-control/db/init.sql

# Verify
ls -la /opt/parental-control/db/init.sql
```

---

## Step 4: Deploy in Portainer

### 4.1 Open Portainer
Navigate to: `http://your-server-ip:9000`

### 4.2 Create Stack
1. Click **"Stacks"** in the left sidebar
2. Click **"+ Add stack"** button
3. **Name:** `parental-control`
4. **Build method:** Select **"Web editor"**

### 4.3 Paste docker-compose.yml

Copy and paste this configuration into the web editor:

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
    image: parental-control-webservice:latest
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

### 4.4 Set Environment Variable
1. Scroll down to **"Environment variables"**
2. Click **"+ Add environment variable"**
3. **Name:** `DB_PASSWORD`
4. **Value:** Choose a strong password (e.g., `MySecurePassword123!`)
5. ⚠️ **Remember this password!**

### 4.5 Deploy
1. Click **"Deploy the stack"** button at the bottom
2. Wait 30-60 seconds for deployment to complete

---

## Step 5: Verify Deployment

Back on your server, check that containers are running:

```bash
# Check container status
docker ps | grep parental-control

# You should see TWO running containers:
# parental-control-db          Up 2 minutes (healthy)
# parental-control-webservice  Up 2 minutes (healthy)

# Check web service logs
docker logs parental-control-webservice

# Look for these messages:
# ✓ "Starting Parental Control Web Service"
# ✓ "Database migrations applied successfully" (or "Database not initialized")
# ✓ "Now listening on: http://[::]:8080"

# Check database logs
docker logs parental-control-db

# Look for:
# ✓ "database system is ready to accept connections"

# Test health endpoint
curl http://localhost:8080/health
# Should return: Healthy
```

---

## Step 6: Access the Web Interface

Open your browser and navigate to:
```
http://your-server-ip:8080
```

### If Database Needs Initialization:
1. You'll be redirected to `/setup` page
2. Click **"Initialize Database"** button
3. Wait for initialization (10-30 seconds)
4. You'll be redirected to the home page

### Create Your First User:
1. Navigate to **"Users"** section
2. Click **"Add User"**
3. Create a **Parent** account (for yourself)
4. Create **Child** accounts (for users to monitor)
5. Set up time profiles for each child

---

## Troubleshooting

### ❌ Error: "pull access denied for parental-control-webservice"

**Cause:** Docker image not loaded before deploying in Portainer.

**Solution:**
```bash
# Load the image
docker load -i webservice-image.tar

# Tag it as latest (replace <hash> with actual hash)
docker tag parental-control-webservice:<hash> parental-control-webservice:latest

# Verify
docker images | grep parental-control

# Then redeploy in Portainer (delete old stack and create new one)
```

### ❌ Container Keeps Restarting

**Check logs:**
```bash
docker logs parental-control-webservice --tail 50
```

**Common causes:**
- **Database not ready yet:** Wait 30 seconds and check again
- **Wrong DB_PASSWORD:** Check environment variable in Portainer
- **Port 8080 already in use:** Change port in docker-compose.yml to `"8081:8080"`

### ❌ Cannot Access Web Interface

**Check firewall:**
```bash
# Allow port 8080
sudo ufw allow 8080/tcp
sudo ufw status

# Test locally
curl http://localhost:8080/health
# Should return: Healthy
```

**Check if service is listening:**
```bash
sudo netstat -tlnp | grep 8080
# Should show docker-proxy listening on port 8080
```

### ❌ Database Connection Failed

**Check database container:**
```bash
docker logs parental-control-db

# Test database connection
docker exec parental-control-db psql -U parental_control_user -d parental_control -c "SELECT 1"
# Should return: 1
```

**Verify password:**
- Check DB_PASSWORD in Portainer stack environment variables
- Make sure it matches in both postgres and webservice containers

### ❌ Permission Denied on init.sql

**Fix permissions:**
```bash
sudo chmod 644 /opt/parental-control/db/init.sql
sudo chown root:root /opt/parental-control/db/init.sql

# Restart the stack in Portainer
```

---

## Updating to a New Version

1. Download new release:
   ```bash
   wget https://github.com/crowndip/timekeeper-net/releases/download/v1.2.0/server-deployment.tar.gz
   tar -xzf server-deployment.tar.gz
   ```

2. Load new image:
   ```bash
   docker load -i webservice-image.tar
   docker tag parental-control-webservice:<new-hash> parental-control-webservice:latest
   ```

3. In Portainer:
   - Go to your stack
   - Click **"Update the stack"**
   - Click **"Pull and redeploy"**

---

## Backup and Restore

### Backup Database

```bash
# Backup to file
docker exec parental-control-db pg_dump -U parental_control_user parental_control > backup.sql

# Backup with timestamp
docker exec parental-control-db pg_dump -U parental_control_user parental_control > backup-$(date +%Y%m%d-%H%M%S).sql
```

### Restore Database

```bash
# Stop the web service
docker stop parental-control-webservice

# Restore from backup
docker exec -i parental-control-db psql -U parental_control_user -d parental_control < backup.sql

# Start the web service
docker start parental-control-webservice
```

---

## Security Recommendations

1. **Strong Password:** Use a strong, unique password for `DB_PASSWORD`
2. **Firewall:** Only allow port 8080 from trusted networks
3. **HTTPS:** Set up a reverse proxy (nginx/Traefik) for HTTPS - see `REVERSE_PROXY.md`
4. **Backups:** Schedule regular database backups
5. **Updates:** Keep Docker and Portainer updated

---

## Quick Reference Commands

```bash
# View logs
docker logs -f parental-control-webservice
docker logs -f parental-control-db

# Restart services
docker restart parental-control-webservice
docker restart parental-control-db

# Check status
docker ps | grep parental-control

# Access database
docker exec -it parental-control-db psql -U parental_control_user -d parental_control

# Check disk usage
docker system df

# Remove old images
docker image prune -a
```

---

## Uninstalling

To completely remove the stack:

1. In Portainer: **Stacks** → Select **"parental-control"** → **"Delete this stack"**
2. Check **"Remove associated volumes"** to delete all data
3. Click **"Delete"**

Or via command line:
```bash
# Stop and remove containers
docker stop parental-control-webservice parental-control-db
docker rm parental-control-webservice parental-control-db

# Remove volumes (deletes all data!)
docker volume rm parental-control_postgres_data

# Remove network
docker network rm parental-control_parental-control-network

# Remove image
docker rmi parental-control-webservice:latest
```

---

## Summary - Complete Command Sequence

```bash
# 1. Download and extract
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.1.0/server-deployment.tar.gz
tar -xzf server-deployment.tar.gz

# 2. Load and tag Docker image
docker load -i webservice-image.tar
docker tag parental-control-webservice:<hash> parental-control-webservice:latest

# 3. Prepare database init script
sudo mkdir -p /opt/parental-control/db
sudo cp init.sql /opt/parental-control/db/init.sql

# 4. Deploy in Portainer (via web UI)
#    - Paste docker-compose.yml
#    - Set DB_PASSWORD environment variable
#    - Click "Deploy the stack"

# 5. Verify
docker ps | grep parental-control
docker logs parental-control-webservice

# 6. Access
# http://your-server-ip:8080
```

---

## Support

- **Detailed Guide:** See `PORTAINER_DEPLOYMENT_DETAILED.md`
- **Documentation:** See `README.md`
- **Issues:** https://github.com/crowndip/timekeeper-net/issues
- **Logs:** Always check `docker logs` when troubleshooting

---

## ⚠️ Most Common Mistake

**Forgetting to load the Docker image before deploying in Portainer!**

Always run these commands FIRST:
```bash
docker load -i webservice-image.tar
docker tag parental-control-webservice:<hash> parental-control-webservice:latest
```

Then deploy in Portainer.

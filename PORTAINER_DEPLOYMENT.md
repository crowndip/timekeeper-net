# Portainer Deployment Guide

## Prerequisites

- Portainer installed and running
- Docker image built: `parental-control-webservice:latest`

## Step 1: Build Docker Image

Build the web service image before deploying to Portainer:

```bash
cd /config/timekeeper-net
docker build -f docker/Dockerfile.webservice -t parental-control-webservice:latest .
```

## Step 2: Prepare init.sql

Copy the database initialization script to a location accessible by Portainer:

```bash
# Option 1: Use bind mount (recommended)
mkdir -p /opt/parental-control/db
cp docker/init.sql /opt/parental-control/db/init.sql
```

Update the compose file volume path to:
```yaml
- /opt/parental-control/db/init.sql:/docker-entrypoint-initdb.d/init.sql:ro
```

## Step 3: Deploy via Portainer

### Using Portainer Web UI:

1. **Login to Portainer** (usually http://localhost:9000)

2. **Navigate to Stacks**
   - Click "Stacks" in the left menu
   - Click "+ Add stack"

3. **Create Stack**
   - **Name**: `parental-control`
   - **Build method**: Web editor
   - Paste the contents of `docker-compose.yml`

4. **Set Environment Variables**
   Scroll down to "Environment variables" and add:
   ```
   DB_PASSWORD=your_secure_password_here
   ```

5. **Deploy**
   - Click "Deploy the stack"
   - Wait for containers to start

## Step 4: Verify Deployment

Check the stack status in Portainer:
- Both containers should show "running"
- Green health check indicators

Access the application:
- **Web UI**: http://your-server:8080
- **API Docs**: http://your-server:8080/swagger
- **Health**: http://your-server:8080/health

## Stack Configuration

The stack creates:
- **Network**: `parental-control-net`
- **Volume**: `parental_control_db` (persistent database)
- **Containers**:
  - `parental-control-db` (PostgreSQL)
  - `parental-control-web` (ASP.NET Core)

## Environment Variables

Set in Portainer stack configuration:

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `DB_PASSWORD` | Yes | - | PostgreSQL password |

## Updating the Stack

### Update Web Service:

1. Build new image:
   ```bash
   docker build -f docker/Dockerfile.webservice -t parental-control-webservice:latest .
   ```

2. In Portainer:
   - Go to Stacks → parental-control
   - Click "Update the stack"
   - Enable "Re-pull image and redeploy"
   - Click "Update"

### Update Database Schema:

Database schema is initialized only on first run. To update:

1. Connect to database:
   ```bash
   docker exec -it parental-control-db psql -U pcadmin -d parental_control
   ```

2. Run SQL commands manually or use migrations

## Troubleshooting

### View Logs in Portainer:
1. Go to Containers
2. Click on container name
3. Click "Logs" tab

### Database Connection Issues:
- Check DB_PASSWORD is set correctly
- Verify postgres container is healthy
- Check network connectivity

### Web Service Not Starting:
- Check logs for errors
- Verify database is ready (health check)
- Ensure port 8080 is not in use

## Backup

### Database Backup:
```bash
docker exec parental-control-db pg_dump -U pcadmin parental_control > backup.sql
```

### Restore:
```bash
docker exec -i parental-control-db psql -U pcadmin parental_control < backup.sql
```

## Removal

To remove the stack in Portainer:
1. Go to Stacks → parental-control
2. Click "Delete this stack"
3. Optionally check "Remove associated volumes" to delete data

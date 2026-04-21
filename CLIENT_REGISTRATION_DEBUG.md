# Client Registration Failure - Debugging Guide

## Problem Summary
Client on computer is failing to register with server at `https://tracking.jerhot.eu`

**Error:** HTTP 500 from `/api/client/register` endpoint

**Client Log:**
```
2026-04-22 00:00:00 [ERR] Failed to register with server: InternalServerError
```

## Root Cause Analysis

The client is working correctly and sending proper registration requests. The server is returning HTTP 500, which indicates a server-side error.

## Diagnostic Steps

### 1. Check Server Logs

SSH into the server hosting `tracking.jerhot.eu` and check Docker logs:

```bash
# Check webservice logs
docker logs webservice --tail 100

# Follow logs in real-time
docker logs webservice -f
```

Look for errors around the time of registration attempts (check timestamps in client log).

### 2. Check Database Connection

The most common cause of 500 errors during registration is database connectivity issues:

```bash
# Check if postgres container is running
docker ps | grep postgres

# Check postgres logs
docker logs postgres --tail 50

# Test database connection from webservice
docker exec webservice psql -h postgres -U parentalcontrol -d parentalcontrol -c "SELECT 1;"
```

### 3. Check Database Schema

The `Computers` table might be missing or have wrong schema:

```bash
# Connect to database
docker exec -it postgres psql -U parentalcontrol -d parentalcontrol

# Check if Computers table exists
\dt

# Check Computers table structure
\d "Computers"

# Should have these columns:
# - Id (uuid)
# - Hostname (varchar 255)
# - MachineId (varchar 64)
# - OsInfo (varchar 255)
# - LastSeenAt (timestamp)
# - IsActive (boolean)
# - CreatedAt (timestamp)
# - UpdatedAt (timestamp)
# - ApiKey (varchar 128)
```

### 4. Manual Registration Test

Test the endpoint manually to see the exact error:

```bash
# From the client machine
curl -v -X POST https://tracking.jerhot.eu/api/client/register \
  -H "Content-Type: application/json" \
  -d '{
    "hostname": "test-machine",
    "machineId": "test-123",
    "osInfo": "Linux 5.15"
  }'
```

This should return either:
- **200 OK** with `{"computerId":"...", "apiKey":"..."}` (success)
- **500** with error details in server logs

## Common Issues & Solutions

### Issue 1: Database Not Initialized

**Symptom:** Server logs show "relation 'Computers' does not exist"

**Solution:**
```bash
# Check if database is initialized
docker exec postgres psql -U parentalcontrol -d parentalcontrol -c "\dt"

# If empty, initialize database
docker exec webservice dotnet ef database update
```

### Issue 2: Database Connection String Wrong

**Symptom:** Server logs show "could not connect to server"

**Solution:** Check `docker-compose.yml` environment variables:
```yaml
services:
  webservice:
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=parentalcontrol;Username=parentalcontrol;Password=${DB_PASSWORD}
```

Verify `DB_PASSWORD` matches postgres container password.

### Issue 3: Old Server Version

**Symptom:** Server is running old version without proper error handling

**Solution:**
```bash
# Check current version
curl -s https://tracking.jerhot.eu/ | grep -o "Version v[0-9.]*"

# Should be v1.45.0 or later
# If older, deploy latest version:
curl -fsSL https://raw.githubusercontent.com/crowndip/timekeeper-net/main/scripts/deploy-server.sh | sudo bash
```

### Issue 4: Missing Migration

**Symptom:** Database schema is outdated

**Solution:**
```bash
# Apply pending migrations
docker exec webservice dotnet ef database update

# Or restart containers to auto-apply migrations
docker-compose restart webservice
```

## Expected Behavior

When working correctly:

1. Client sends POST to `/api/client/register` with:
   ```json
   {
     "hostname": "machine-name",
     "machineId": "unique-machine-id",
     "osInfo": "Linux 5.15.0"
   }
   ```

2. Server creates/updates Computer record in database

3. Server responds with:
   ```json
   {
     "computerId": "guid",
     "apiKey": "api-key-string"
   }
   ```

4. Client saves `computerId` to `/etc/parental-control/computer-id`

5. Client uses `computerId` for all subsequent API calls

## After Fixing Server

Once server issue is resolved:

```bash
# On client machine, restart service to retry registration
sudo systemctl restart parental-control-client

# Watch logs to verify success
tail -f /var/log/parental-control/client*.log

# Should see:
# "Registered with server: {guid}"

# Verify computer-id file was created
cat /etc/parental-control/computer-id
```

## Contact

If issue persists after following these steps, provide:
1. Server logs from time of registration attempt
2. Database schema output (`\d "Computers"`)
3. Server version
4. Any error messages from postgres logs

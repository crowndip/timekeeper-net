# Portainer Deployment - Quick Reference

## 🚀 Deployment Steps

### 1. Build Docker Image
```bash
cd /config/timekeeper-net
./scripts/build-docker-image.sh
```

### 2. Prepare Database Init Script
```bash
sudo mkdir -p /opt/parental-control/db
sudo cp docker/init.sql /opt/parental-control/db/init.sql
```

### 3. Deploy in Portainer

1. **Access Portainer**: http://your-server:9000
2. **Create Stack**:
   - Stacks → Add stack
   - Name: `parental-control`
   - Build method: Web editor
3. **Paste docker-compose.yml content**
4. **Add Environment Variable**:
   - Name: `DB_PASSWORD`
   - Value: `your_secure_password`
5. **Deploy the stack**

### 4. Verify
- Check containers are running
- Access: http://localhost:8080

## 📋 Stack Configuration

**Stack Name**: `parental-control`

**Environment Variables**:
```
DB_PASSWORD=your_secure_password
```

**Containers**:
- `parental-control-db` (PostgreSQL 16)
- `parental-control-web` (ASP.NET Core)

**Volumes**:
- `parental_control_db` (persistent database)

**Network**:
- `parental-control-net`

## 🔄 Update Stack

1. Build new image:
   ```bash
   ./scripts/build-docker-image.sh
   ```

2. In Portainer:
   - Stacks → parental-control
   - Update the stack
   - Enable "Re-pull image and redeploy"
   - Update

## 🔍 Troubleshooting

**View Logs**:
- Portainer → Containers → Click container → Logs

**Database Connection**:
```bash
docker exec -it parental-control-db psql -U pcadmin -d parental_control
```

**Restart Stack**:
- Portainer → Stacks → parental-control → Stop/Start

## 💾 Backup

```bash
# Backup database
docker exec parental-control-db pg_dump -U pcadmin parental_control > backup.sql

# Restore
docker exec -i parental-control-db psql -U pcadmin parental_control < backup.sql
```

## 🗑️ Remove Stack

Portainer → Stacks → parental-control → Delete this stack

---

For detailed instructions, see [PORTAINER_DEPLOYMENT.md](PORTAINER_DEPLOYMENT.md)

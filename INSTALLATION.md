# Installation Guide

Complete installation instructions for Parental Control System.

## Server Installation

### Prerequisites
- Docker and Docker Compose
- Portainer (recommended) or Docker CLI
- 2GB RAM minimum
- 10GB disk space

### Portainer Deployment (Recommended)

1. **Download server deployment package**:
   ```bash
   wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.1/server-deployment.tar.gz
   tar -xzf server-deployment.tar.gz
   cd server-deployment
   ```

2. **Load Docker image**:
   ```bash
   docker load -i webservice-image.tar
   ```

3. **Deploy in Portainer**:
   - Open Portainer web UI
   - Go to **Stacks** → **Add stack**
   - Name: `parental-control`
   - Copy contents of `docker-compose.yml` into editor
   - Add environment variable: `DB_PASSWORD=your_secure_password`
   - Click **Deploy the stack**

4. **Verify deployment**:
   - Open http://your-server-ip:8080
   - You should see the admin dashboard

See [PORTAINER_DEPLOYMENT.md](PORTAINER_DEPLOYMENT.md) for detailed instructions.

### Docker CLI Deployment

```bash
# Set database password
export DB_PASSWORD=your_secure_password

# Start services
docker-compose up -d

# Check logs
docker-compose logs -f webservice
```

## Client Installation

### Ubuntu/Debian (Recommended)

**Prerequisites**: Ubuntu 20.04+, Debian 11+, or compatible

1. **Download .deb package**:
   ```bash
   wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.1/parental-control-client_1.0.1_amd64.deb
   ```

2. **Install package**:
   ```bash
   sudo dpkg -i parental-control-client_1.0.1_amd64.deb
   ```
   
   If you get dependency errors:
   ```bash
   sudo apt-get install -f
   ```

3. **Configure server URL**:
   ```bash
   sudo nano /opt/parental-control/appsettings.json
   ```
   
   Update the `ServerUrl`:
   ```json
   {
     "ParentalControl": {
       "ServerUrl": "http://your-server-ip:8080",
       "TickIntervalSeconds": 60,
       "OfflineMode": {
         "Enabled": true,
         "CacheDurationMinutes": 1440
       }
     }
   }
   ```

4. **Start service**:
   ```bash
   sudo systemctl start parental-control-client
   ```

5. **Enable auto-start** (already enabled by package):
   ```bash
   sudo systemctl enable parental-control-client
   ```

6. **Check status**:
   ```bash
   sudo systemctl status parental-control-client
   ```

7. **View logs**:
   ```bash
   sudo journalctl -u parental-control-client -f
   ```

### Generic Linux (Other Distributions)

**Prerequisites**: systemd-based Linux distribution

1. **Download tarball**:
   ```bash
   wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.1/client-linux-x64.tar.gz
   tar -xzf client-linux-x64.tar.gz
   cd client-linux-x64
   ```

2. **Run installation script**:
   ```bash
   sudo ./install-client.sh
   ```
   
   This script:
   - Copies binaries to `/opt/parental-control/`
   - Installs systemd service
   - Sets proper permissions

3. **Configure server URL**:
   ```bash
   sudo nano /opt/parental-control/appsettings.json
   ```
   
   Update the `ServerUrl`:
   ```json
   {
     "ParentalControl": {
       "ServerUrl": "http://your-server-ip:8080",
       "TickIntervalSeconds": 60
     }
   }
   ```

4. **Start service**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable parental-control-client
   sudo systemctl start parental-control-client
   ```

5. **Check status**:
   ```bash
   sudo systemctl status parental-control-client
   ```

### Build from Source

**Prerequisites**: .NET 10 SDK

1. **Clone repository**:
   ```bash
   git clone https://github.com/crowndip/timekeeper-net.git
   cd timekeeper-net
   ```

2. **Build client**:
   ```bash
   dotnet publish src/ParentalControl.Client/ParentalControl.Client.csproj \
     -c Release \
     -r linux-x64 \
     --self-contained \
     -o /tmp/client-build
   ```

3. **Install**:
   ```bash
   sudo mkdir -p /opt/parental-control
   sudo cp -r /tmp/client-build/* /opt/parental-control/
   sudo cp scripts/parental-control-client.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable parental-control-client
   ```

4. **Configure and start** (see steps 3-5 above)

## Post-Installation

### Server Configuration

1. **Access admin UI**: http://your-server-ip:8080

2. **Create user accounts**:
   - Go to **Users** page
   - Add users with account types:
     - **Child**: Supervised with time limits
     - **Parent**: No limits
     - **Technical**: System accounts, no limits

3. **Create time profiles**:
   - Go to **Profiles** page
   - Set daily/weekly limits
   - Configure allowed hours

4. **Assign profiles to users**

### Client Verification

1. **Check client registration**:
   ```bash
   sudo journalctl -u parental-control-client -n 50
   ```
   
   Look for: `Computer registered successfully`

2. **Verify server communication**:
   - Check admin UI → **Computers** page
   - Your client should appear in the list

3. **Test enforcement**:
   - Create a test user with 1-minute limit
   - Log in as that user
   - Wait for enforcement (logout/lock)

## Troubleshooting

### Client can't connect to server

```bash
# Check service status
sudo systemctl status parental-control-client

# Check logs
sudo journalctl -u parental-control-client -f

# Test network connectivity
curl http://your-server-ip:8080/health

# Verify configuration
cat /opt/parental-control/appsettings.json
```

### Server not accessible

```bash
# Check Docker containers
docker ps

# Check logs
docker logs parental-control-webservice-1

# Check database
docker logs parental-control-postgres-1

# Verify port binding
netstat -tlnp | grep 8080
```

### Offline mode not working

Check client configuration:
```json
{
  "ParentalControl": {
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    }
  }
}
```

Restart service after changes:
```bash
sudo systemctl restart parental-control-client
```

## Uninstallation

### Ubuntu/Debian

```bash
sudo dpkg -r parental-control-client
sudo rm -rf /opt/parental-control
```

### Generic Linux

```bash
sudo systemctl stop parental-control-client
sudo systemctl disable parental-control-client
sudo rm /etc/systemd/system/parental-control-client.service
sudo systemctl daemon-reload
sudo rm -rf /opt/parental-control
```

### Server

```bash
# Portainer: Delete stack from web UI

# Docker CLI:
docker-compose down -v
```

## Upgrading

### Client (Ubuntu/Debian)

```bash
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.2/parental-control-client_1.0.2_amd64.deb
sudo dpkg -i parental-control-client_1.0.2_amd64.deb
sudo systemctl restart parental-control-client
```

### Client (Generic Linux)

```bash
sudo systemctl stop parental-control-client
# Download and extract new version
sudo rm -rf /opt/parental-control/bin
sudo cp -r client-linux-x64/* /opt/parental-control/
sudo systemctl start parental-control-client
```

### Server

```bash
# Download new server-deployment.tar.gz
docker load -i webservice-image.tar
# Update stack in Portainer or restart with docker-compose
docker-compose up -d
```

## Support

- **Documentation**: https://github.com/crowndip/timekeeper-net
- **Issues**: https://github.com/crowndip/timekeeper-net/issues
- **Releases**: https://github.com/crowndip/timekeeper-net/releases

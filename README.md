# Parental Control System - .NET 8

A centralized parental control system for Linux with time tracking, enforcement, and web-based administration.

## Architecture

- **Central Server**: ASP.NET Core 8 web service with PostgreSQL database
- **Client Agent**: .NET 8 worker service running on Linux clients
- **Admin UI**: Blazor Server web interface
- **Client UI**: Avalonia cross-platform GUI for notifications

## Features

- ✅ Centralized time management across multiple computers
- ✅ Daily and weekly time limits
- ✅ Allowed hours configuration
- ✅ Automatic enforcement (logout/lock)
- ✅ Real-time usage tracking
- ✅ Web-based administration
- ✅ Offline mode support
- ✅ Cross-platform client UI

## Quick Start

### Server Deployment (Portainer)

```bash
# 1. Build Docker image
./scripts/build-docker-image.sh

# 2. Prepare database init script
sudo mkdir -p /opt/parental-control/db
sudo cp docker/init.sql /opt/parental-control/db/init.sql

# 3. Deploy via Portainer Web UI
# - Create new stack named "parental-control"
# - Paste docker-compose.yml content
# - Set environment variable: DB_PASSWORD=your_secure_password
# - Deploy

# 4. Access admin UI
open http://localhost:8080
```

See [PORTAINER_DEPLOYMENT.md](PORTAINER_DEPLOYMENT.md) for detailed instructions.

### Client Installation

#### Option 1: Ubuntu/Debian (.deb package) - Recommended

Download the latest `.deb` package from [Releases](https://github.com/crowndip/timekeeper-net/releases):

```bash
# Download and install
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.1/parental-control-client_1.0.1_amd64.deb
sudo dpkg -i parental-control-client_1.0.1_amd64.deb

# Configure server URL
sudo nano /opt/parental-control/appsettings.json
# Set "ServerUrl": "http://your-server-ip:8080"

# Start service
sudo systemctl start parental-control-client

# Check status
sudo systemctl status parental-control-client
```

**Dependencies**: Automatically installs `systemd` and `libicu` libraries.

#### Option 2: Generic Linux (tar.gz)

Download the latest `client-linux-x64.tar.gz` from [Releases](https://github.com/crowndip/timekeeper-net/releases):

```bash
# Download and extract
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.0.1/client-linux-x64.tar.gz
tar -xzf client-linux-x64.tar.gz
cd client-linux-x64

# Install
sudo ./install-client.sh

# Configure server URL
sudo nano /opt/parental-control/appsettings.json
# Set "ServerUrl": "http://your-server-ip:8080"

# Start service
sudo systemctl start parental-control-client

# Check status
sudo systemctl status parental-control-client
```

#### Option 3: Build from Source

```bash
# Build client
cd src/ParentalControl.Client
dotnet publish -c Release -r linux-x64 --self-contained

# Install
cd ../../scripts
sudo ./install-client.sh
```

## Project Structure

```
ParentalControl.sln
├── src/
│   ├── ParentalControl.Shared/       # DTOs and shared models
│   ├── ParentalControl.WebService/   # ASP.NET Core API + Blazor UI
│   ├── ParentalControl.Client/       # Background service agent
│   └── ParentalControl.Client.UI/    # Avalonia notification UI
├── docker/
│   ├── Dockerfile.webservice
│   └── init.sql
├── scripts/
│   ├── install-client.sh
│   └── parental-control-client.service
└── docker-compose.yml
```

## Requirements

- .NET 8 SDK
- PostgreSQL 16+
- Docker & Docker Compose (for server)
- Linux with systemd (for client)

## Configuration

### Server

Edit `docker-compose.yml` or set environment variables:
- `DB_PASSWORD`: PostgreSQL password
- `ConnectionStrings__DefaultConnection`: Database connection string

### Client

Edit `/opt/parental-control/appsettings.json`:
```json
{
  "ParentalControl": {
    "ServerUrl": "http://your-server:8080",
    "TickIntervalSeconds": 60
  }
}
```

## API Documentation

Once running, access Swagger UI at: `http://localhost:8080/swagger`

## Development

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run web service locally
cd src/ParentalControl.WebService
dotnet run

# Run client locally
cd src/ParentalControl.Client
dotnet run
```

## License

See DevSpecsRewrite.md for full specifications.

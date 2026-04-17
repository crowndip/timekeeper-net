# Parental Control System - .NET 8

**Version**: v1.4.0  
**Status**: ✅ Production Ready

A centralized parental control system for Linux and Windows with time tracking, enforcement, and web-based administration.

## Architecture

- **Central Server**: ASP.NET Core 8 web service with PostgreSQL database
- **Client Agent**: .NET 8 worker service running on Linux and Windows
- **Admin UI**: Blazor Server web interface with authentication
- **Client UI**: Avalonia (Linux) and WPF (Windows) for notifications

## Features

### Core Functionality
- ✅ Centralized time management across multiple computers
- ✅ Cross-platform support (Linux and Windows)
- ✅ Time limits shared across all devices
- ✅ Per-day time limits (Monday-Sunday)
- ✅ Weekly time limits
- ✅ Allowed hours configuration
- ✅ Automatic enforcement (logout/lock)
- ✅ Real-time usage tracking
- ✅ Offline mode support
- ✅ Native UI for each platform

### Security & Administration (v1.4.0)
- ✅ **Dashboard Authentication** - Password-protected admin interface
- ✅ **Two-Tier Security** - Separate passwords for viewing and editing
- ✅ **Read-Only Mode** - View-only access by default
- ✅ **Emergency Time Adjustment** - Grant extra time for exceptional situations
- ✅ **Input Validation** - Comprehensive validation on all inputs
- ✅ **Username Normalization** - Case-insensitive user identification

### User Management
- ✅ **Auto-User Creation** - Users automatically created on first login
- ✅ **Account Types** - Parent, Child, Unassigned
- ✅ **Multiple Profiles** - Different time limits per user
- ✅ **Cross-Device Tracking** - Same user across Linux and Windows

## Quick Start

### Server Deployment (Docker Compose)

```bash
# 1. Set environment variables
export ADMIN_PASSWORD=your_dashboard_password
export LIMIT_ADMIN_PASSWORD=your_admin_operations_password
export DB_PASSWORD=your_database_password

# 2. Start services
docker-compose up -d

# 3. Access admin UI
open http://localhost:8080

# 4. Login with AdminPassword
# Default: "admin" (change via environment variable)
```

See [PORTAINER_DEPLOYMENT.md](PORTAINER_DEPLOYMENT.md) for Portainer deployment instructions.

### Client Installation

#### Linux Client

##### Option 1: Generic Linux (tar.gz)

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

##### Option 2: Build from Source

```bash
# Build client
cd src/ParentalControl.Client
dotnet publish -c Release -r linux-x64 --self-contained

# Install
cd ../../scripts
sudo ./install-client.sh
```

#### Windows Client

Download the latest `client-windows-x64.zip` from [Releases](https://github.com/crowndip/timekeeper-net/releases):

```powershell
# Download and extract
# Open PowerShell as Administrator

# Install
.\install-windows-client.ps1 -ServerUrl "http://your-server-ip:8080"

# Check service status
Get-Service ParentalControlClient

# View logs
Get-Content "C:\ProgramData\ParentalControl\Logs\client-*.log" -Tail 50
```

See [WINDOWS_CLIENT.md](WINDOWS_CLIENT.md) for detailed Windows installation instructions.

## Cross-Platform Time Sharing

Time limits are shared across all devices:

**Example:**
- User "john" has 120 minutes/day limit
- Uses Linux PC for 30 minutes → 90 minutes remaining
- Switches to Windows PC → Server reports 90 minutes remaining
- Uses Windows for 90 minutes → 0 minutes remaining
- Both clients enforce the limit

## Project Structure

```
ParentalControl.sln
├── src/
│   ├── ParentalControl.Shared/              # DTOs and shared models
│   ├── ParentalControl.WebService/          # ASP.NET Core API + Blazor UI
│   ├── ParentalControl.Client/              # Linux background service
│   ├── ParentalControl.Client.UI/           # Avalonia UI (Linux)
│   ├── ParentalControl.Client.Windows/      # Windows background service
│   └── ParentalControl.Client.Windows.UI/   # WPF UI (Windows)
├── docker/
│   ├── Dockerfile.webservice
│   └── init.sql
├── scripts/
│   ├── install-client.sh                    # Linux installer
│   ├── install-windows-client.ps1           # Windows installer
│   ├── uninstall-windows-client.ps1         # Windows uninstaller
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
- `AdminPassword`: Dashboard login password (default: "admin")
- `LimitAdministratorPassword`: Administrator operations password
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

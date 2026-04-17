# Quick Reference Guide

## 🚀 Getting Started

### 1. Install .NET 10 SDK
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
```

### 2. Start the Server (Development)
```bash
# Start PostgreSQL
docker-compose up -d postgres

# Run web service
cd src/ParentalControl.WebService
dotnet run
```

### 3. Start the Server (Portainer)
```bash
# Build Docker image
./scripts/build-docker-image.sh

# Prepare database init script
sudo mkdir -p /opt/parental-control/db
sudo cp docker/init.sql /opt/parental-control/db/init.sql

# Deploy via Portainer Web UI
# See PORTAINER_DEPLOYMENT.md for details
```

### 4. Build Client Agent
```bash
cd src/ParentalControl.Client
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

### 5. Build Client UI
```bash
cd src/ParentalControl.Client.UI
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

## 📡 API Endpoints

### Client Endpoints
- `POST /api/client/register` - Register computer
- `POST /api/client/session/start` - Start session
- `POST /api/client/usage` - Report usage
- `POST /api/client/session/end` - End session
- `GET /api/client/config/{id}` - Get config

### System Endpoints
- `GET /health` - Health check
- `GET /swagger` - API documentation

## 🗄️ Database

### Connect to PostgreSQL
```bash
docker exec -it parental-control-db psql -U pcadmin -d parental_control
```

### Common Queries
```sql
-- List all users
SELECT * FROM users;

-- List active sessions
SELECT * FROM sessions WHERE is_active = true;

-- Today's usage
SELECT u.username, tu.minutes_used 
FROM time_usage tu 
JOIN users u ON tu.user_id = u.id 
WHERE tu.usage_date = CURRENT_DATE;
```

## 🔧 Troubleshooting

### Check Service Status
```bash
# Web service
docker-compose ps
docker-compose logs webservice

# Client agent
sudo systemctl status parental-control-client
sudo journalctl -u parental-control-client -f
```

### Reset Database
```bash
docker-compose down -v
docker-compose up -d
```

### Rebuild Docker Images
```bash
docker-compose build --no-cache
docker-compose up -d
```

## 📝 Development Commands

### Restore Dependencies
```bash
dotnet restore
```

### Build Solution
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Create Migration
```bash
cd src/ParentalControl.WebService
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Format Code
```bash
dotnet format
```

## 🎯 Key Files

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Server orchestration |
| `src/ParentalControl.WebService/Program.cs` | Web service entry |
| `src/ParentalControl.Client/Program.cs` | Client agent entry |
| `docker/init.sql` | Database schema |
| `scripts/install-client.sh` | Client installer |

## 🔐 Security Notes

- Change default database password in `.env`
- Use HTTPS in production
- Secure API keys for clients
- Run client agent as root (required for enforcement)
- Review systemd service security settings

## 📊 Monitoring

### View Logs
```bash
# Server logs
docker-compose logs -f webservice

# Client logs
sudo tail -f /var/log/parental-control/client.log

# Database logs
docker-compose logs -f postgres
```

### Check Health
```bash
curl http://localhost:8080/health
```

## 🌐 URLs

- **Admin UI**: http://localhost:8080
- **API Docs**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health
- **Database**: localhost:5432

## 📦 Project Stats

- **4 Projects**: Shared, WebService, Client, Client.UI
- **24 Code Files**: .cs, .csproj, .sln
- **~265 Lines of Code** (excluding specs)
- **Cross-platform**: Linux, Windows, macOS (UI)

## 🎨 UI Framework

**Avalonia UI** - Cross-platform .NET UI framework
- Modern, native-looking UI
- XAML-based design
- MVVM architecture with ReactiveUI
- Runs on Linux, Windows, macOS

## 🔄 Workflow

1. User logs in → Client detects session
2. Client tracks time every minute
3. Client reports to server
4. Server calculates remaining time
5. Server checks limits
6. Client enforces if needed
7. Admin views reports in web UI

## 💡 Tips

- Use `dotnet watch run` for hot reload during development
- Check Swagger UI for API testing
- Use MudBlazor components for admin UI
- Implement D-Bus integration for full session monitoring
- Add authentication before production deployment

---

**Need help?** Check PROJECT_SUMMARY.md for detailed information.

# Build and Run Instructions

## Prerequisites

Install .NET 10 SDK:
```bash
# For Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

## Build the Solution

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests (if any)
dotnet test
```

## Run the Web Service Locally

```bash
cd src/ParentalControl.WebService
dotnet run
```

Access at: http://localhost:5000

## Run the Client Agent Locally

```bash
cd src/ParentalControl.Client
dotnet run
```

## Build for Production

### Web Service (Docker)
```bash
docker-compose build
docker-compose up -d
```

### Client Agent (Linux)
```bash
cd src/ParentalControl.Client
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

### Client UI (Linux)
```bash
cd src/ParentalControl.Client.UI
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

## Database Migrations

```bash
cd src/ParentalControl.WebService

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

## Troubleshooting

### Port already in use
Change the port in `appsettings.json` or use:
```bash
dotnet run --urls "http://localhost:5001"
```

### Database connection failed
Check PostgreSQL is running:
```bash
docker-compose ps
docker-compose logs postgres
```

### Client can't connect to server
Update `appsettings.json` in the client with correct ServerUrl.

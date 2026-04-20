filip@main:~$ curl -fsSL https://raw.githubusercontent.com/crowndip/timekeeper-net/main/scripts/deploy-server.sh | sudo bash
[sudo] password for filip: 
╔══════════════════════════════════════════════════════════════════════╗
║     Parental Control Server - Automated Deployment                  ║
╚══════════════════════════════════════════════════════════════════════╝

📡 Fetching latest release version...
✅ Latest version: v1.21.0

📥 Downloading webservice-image.tar.gz...
   From: https://github.com/crowndip/timekeeper-net/releases/download/v1.21.0/webservice-image.tar.gz
  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current
                                 Dload  Upload   Total   Spent    Left  Speed
  0     0    0     0    0     0      0      0 --:--:-- --:--:-- --:--:--     0
100 97.4M  100 97.4M    0     0  10.8M      0  0:00:09  0:00:09 --:--:-- 10.9M
✅ Download complete

📦 Extracting archive...
✅ Extracted successfully

🐳 Loading Docker image...
Loaded image: parental-control-webservice:1.21.0
✅ Image loaded with tag: 1.21.0

🏷️  Tagging image as 'latest'...
✅ Tagged successfully

🔍 Verifying Docker images...
WARNING: This output is designed for human readability. For machine-readable output, please use --format.
parental-control-webservice:07fee377218d9997e889a22415046a52b4c19d57   11fbe27cd4b6        251MB             0B        
parental-control-webservice:1.14.0                                     b86c434b2071        257MB             0B        
parental-control-webservice:1.14.1                                     d9a74a0e9807        257MB             0B        
parental-control-webservice:1.16.0                                     5d4bf76ab567        257MB             0B        
parental-control-webservice:1.17.0                                     379815defc4b        257MB             0B        
parental-control-webservice:1.20.0                                     2f9cb3213cf4        257MB             0B        
parental-control-webservice:1.21.0                                     e09ecee51ba7        257MB             0B   U    
parental-control-webservice:3862bdc781276f546f8ac856219222035e755b69   c94efb2e0d83        251MB             0B        
parental-control-webservice:39726e6c0816b620d95cc95c23402756dc474627   20ab7c503874        251MB             0B        
parental-control-webservice:3bc9126e0f620250aa3506827ddb69477b24af69   84853b9ef46e        251MB             0B        
parental-control-webservice:5748e3f28e41f9a099dd5e9d0ca0d86e995c3019   72bf83531c02        257MB             0B        
parental-control-webservice:6fa934e783bf2af57055f90cc79a7a4a36a4fd4f   d00ef0700d9d        251MB             0B        
parental-control-webservice:874b8b01215964a1b8fbbe7a1bfee20b6c41575f   2bad3c92b361        251MB             0B        
parental-control-webservice:a80633c6102315b1e0bd66eac2e24d82609af359   cbbaec4f6236        251MB             0B        
parental-control-webservice:ae959dc13f54ef7d55cba59cbacabe48fbddf803   6b951e08ee38        251MB             0B        
parental-control-webservice:b1451f3396e3699b21f1e59d780d1e50330454b8   00a3d6efe614        251MB             0B        
parental-control-webservice:b79f46fac6507466a36520a493ce057a76dede4b   3c2927c448b7        251MB             0B        
parental-control-webservice:d0e80e52c482722c51799b85cfcea152c134544d   a02cd76c59b7        257MB             0B        
parental-control-webservice:ea179abea44815b1ea1b89bdb6dc3164b154f9b7   afe3a7a097f2        257MB             0B        
parental-control-webservice:fd17f86f7acc17f2cffbcafaadfcb07dd638940b   f4c63456ddce        251MB             0B        
parental-control-webservice:latest                                     e09ecee51ba7        257MB             0B   U    

🧹 Cleaning up temporary files...
✅ Cleanup complete

╔══════════════════════════════════════════════════════════════════════╗
║                    ✅ DEPLOYMENT SUCCESSFUL! ✅                      ║
╚══════════════════════════════════════════════════════════════════════╝

📋 NEXT STEPS:

1. Go to Portainer: http://your-portainer-url:9000
2. Navigate to: Stacks → parental-control
3. Click 'Update the stack'
4. Click 'Update' (no changes needed)
5. Portainer will pull the new 'latest' image and restart

🌐 Access your server at: http://localhost:8080

📚 First time setup?
   - Copy docker-compose.yml from GitHub release page
   - Create new stack in Portainer
   - Set DB_PASSWORD environment variable
   - Deploy stack
   - Visit http://localhost:8080/setup to initialize database

   -------------------------

   Configuration
Database schema and system information

Database Schema
Current Schema Version
20260417
Expected Schema Version
20260417
Applied Migrations
✅ 20260417173113_InitialCreate
Migrations in Code
📄 20260417173113_InitialCreate
🚨 Docker Image is Outdated!
The running application only has 1 migration(s) compiled in.

Expected: 2 migrations (InitialCreate + AddUserAliases).

Solution: Rebuild Docker image with latest code

docker-compose down
docker-compose build --no-cache webservice
docker-compose up -d
🔒 Authentication Required
Enter administrator password to apply migrations

------------------------------------

When i enter the administrator password it says that an error has occured
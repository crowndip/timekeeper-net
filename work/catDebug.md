

filip@main:~$ curl -fsSL https://raw.githubusercontent.com/crowndip/timekeeper-net/main/scripts/deploy-server.sh | sudo bash
╔══════════════════════════════════════════════════════════════════════╗
║     Parental Control Server - Automated Deployment                  ║
╚══════════════════════════════════════════════════════════════════════╝

📡 Fetching latest release version...
✅ Latest version: v1.14.0

📥 Downloading webservice-image.tar.gz...
   From: https://github.com/crowndip/timekeeper-net/releases/download/v1.14.0/webservice-image.tar.gz
  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current
                                 Dload  Upload   Total   Spent    Left  Speed
  0     0    0     0    0     0      0      0 --:--:-- --:--:-- --:--:--     0
100 97.4M  100 97.4M    0     0   9.9M      0  0:00:09  0:00:09 --:--:--  9.9M
✅ Download complete

📦 Extracting archive...
✅ Extracted successfully

🐳 Loading Docker image...
Loaded image: parental-control-webservice:1.14.0
✅ Image loaded with hash: 1

⚠️  Image 'parental-control-webservice:latest' already exists.

❌ Deployment cancelled.
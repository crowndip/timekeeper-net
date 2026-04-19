filip@main:~$ # Download the script
wget https://github.com/crowndip/timekeeper-net/releases/download/v1.13.0/deploy-server.sh

# Make it executable
chmod +x deploy-server.sh

# Run with sudo
sudo ./deploy-server.sh
--2026-04-19 19:28:07--  https://github.com/crowndip/timekeeper-net/releases/download/v1.13.0/deploy-server.sh
Resolving github.com (github.com)... 140.82.121.4
Connecting to github.com (github.com)|140.82.121.4|:443... connected.
HTTP request sent, awaiting response... 302 Found
Location: https://release-assets.githubusercontent.com/github-production-release-asset/1213234941/3407901a-0bcb-4218-a389-cd019069a7df?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-04-19T20%3A03%3A35Z&rscd=attachment%3B+filename%3Ddeploy-server.sh&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-04-19T19%3A02%3A36Z&ske=2026-04-19T20%3A03%3A35Z&sks=b&skv=2018-11-09&sig=MWQdkiXogvI4abn2FKciU4Tsy%2B9x44BvFdLLwn8CZDw%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3NjYyNzE4NywibmJmIjoxNzc2NjI2ODg3LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.pAwCGG1ACVrd1dAjDElu1jiqiKZo592tcoqrK8b4XkM&response-content-disposition=attachment%3B%20filename%3Ddeploy-server.sh&response-content-type=application%2Foctet-stream [following]
--2026-04-19 19:28:08--  https://release-assets.githubusercontent.com/github-production-release-asset/1213234941/3407901a-0bcb-4218-a389-cd019069a7df?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-04-19T20%3A03%3A35Z&rscd=attachment%3B+filename%3Ddeploy-server.sh&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-04-19T19%3A02%3A36Z&ske=2026-04-19T20%3A03%3A35Z&sks=b&skv=2018-11-09&sig=MWQdkiXogvI4abn2FKciU4Tsy%2B9x44BvFdLLwn8CZDw%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3NjYyNzE4NywibmJmIjoxNzc2NjI2ODg3LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.pAwCGG1ACVrd1dAjDElu1jiqiKZo592tcoqrK8b4XkM&response-content-disposition=attachment%3B%20filename%3Ddeploy-server.sh&response-content-type=application%2Foctet-stream
Resolving release-assets.githubusercontent.com (release-assets.githubusercontent.com)... 185.199.108.133, 185.199.109.133, 185.199.110.133, ...
Connecting to release-assets.githubusercontent.com (release-assets.githubusercontent.com)|185.199.108.133|:443... connected.
HTTP request sent, awaiting response... 200 OK
Length: 4563 (4.5K) [application/octet-stream]
Saving to: ‘deploy-server.sh.3’

deploy-server.sh.3                                       100%[================================================================================================================================>]   4.46K  --.-KB/s    in 0s      

2026-04-19 19:28:08 (38.2 MB/s) - ‘deploy-server.sh.3’ saved [4563/4563]

[sudo] password for filip: 
╔══════════════════════════════════════════════════════════════════════╗
║     Parental Control Server - Automated Deployment                  ║
╚══════════════════════════════════════════════════════════════════════╝

📡 Fetching latest release version...
✅ Latest version: v1.14.0

📥 Downloading webservice-image.tar.gz...
   From: https://github.com/crowndip/timekeeper-net/releases/download/v1.14.0/webservice-image.tar.gz
  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current
                                 Dload  Upload   Total   Spent    Left  Speed
100     9  100     9    0     0     39      0 --:--:-- --:--:-- --:--:--    39
✅ Download complete

📦 Extracting archive...

gzip: stdin: not in gzip format
tar: Child returned status 1
tar: Error is not recoverable: exiting now
Configuration
Database schema and system information

Database Schema
Current Schema Version
20260420
Expected Schema Version
20260417
Applied Migrations
✅ 20260417173113_InitialCreate
✅ 20260420082000_AddUserAliases
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
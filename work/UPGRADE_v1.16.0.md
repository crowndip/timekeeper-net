# Upgrade to v1.16.0 (Alias Feature)

## What's New
- User alias functionality (merge multiple OS usernames)
- Comprehensive test coverage (116 tests)
- Database migration for alias support

## Upgrade Steps

### 1. Pull Latest Code
```bash
cd /config/timekeeper-net
git pull origin main
git checkout v1.16.0
```

### 2. Rebuild Docker Image
```bash
docker-compose down
docker-compose build webservice
docker-compose up -d
```

### 3. Verify Migration Applied
```bash
docker-compose logs webservice | grep -i migration
```

You should see:
```
Applied migration '20260417173113_InitialCreate'
Applied migration '20260420082000_AddUserAliases'
```

### 4. Verify Database Schema
```bash
docker-compose exec postgres psql -U parentalcontrol -d parentalcontrol -c "\d \"Users\""
```

You should see `PrimaryUserId` column.

### 5. Test Alias Feature
1. Open web UI: http://your-server:8080
2. Go to Users tab
3. Click "👥 Aliases" button on any user
4. Add an alias

## Troubleshooting

### Error: "column u.PrimaryUserId does not exist"

**Cause**: Migration not applied (old Docker image)

**Solution**:
```bash
# Force rebuild
docker-compose build --no-cache webservice
docker-compose up -d webservice
```

### Manual Migration (if auto-migration fails)

```bash
docker-compose exec postgres psql -U parentalcontrol -d parentalcontrol
```

Then run:
```sql
ALTER TABLE "Users" ADD COLUMN "PrimaryUserId" uuid NULL;
ALTER TABLE "Users" ADD CONSTRAINT "FK_Users_Users_PrimaryUserId" 
    FOREIGN KEY ("PrimaryUserId") REFERENCES "Users" ("Id") 
    ON DELETE RESTRICT;
CREATE INDEX "IX_Users_PrimaryUserId" ON "Users" ("PrimaryUserId");
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420082000_AddUserAliases', '8.0.26');
```

## Rollback (if needed)

```bash
git checkout v1.15.0
docker-compose build webservice
docker-compose up -d
```

Note: This will not remove the PrimaryUserId column. To fully rollback:
```sql
ALTER TABLE "Users" DROP COLUMN "PrimaryUserId";
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420082000_AddUserAliases';
```

## Version Info

- **Version**: v1.16.0
- **Release Date**: 2026-04-20
- **Migration**: 20260420082000_AddUserAliases
- **Breaking Changes**: None (backward compatible)

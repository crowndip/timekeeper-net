#!/bin/bash
# Apply AddUserAliases migration directly to database

set -e

echo "Applying AddUserAliases migration..."

# Find the database container
CONTAINER=$(docker ps --filter "name=postgres" --filter "name=db" --format "{{.Names}}" | head -1)

if [ -z "$CONTAINER" ]; then
    echo "Error: Could not find PostgreSQL container"
    exit 1
fi

echo "Using container: $CONTAINER"

# Try common database users and database names
for DB_USER in pcadmin parentalcontrol postgres parental_control; do
    for DB_NAME in parental_control parentalcontrol postgres; do
        echo "Trying user: $DB_USER, database: $DB_NAME"
        if docker exec -i "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "SELECT 1;" 2>/dev/null; then
            echo "✅ Connected as $DB_USER to $DB_NAME"
            
            # Apply migration
            docker exec -i "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" <<'EOF'
-- Check if column already exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'PrimaryUserId'
    ) THEN
        -- Add column
        ALTER TABLE "Users" ADD COLUMN "PrimaryUserId" uuid NULL;
        
        -- Create index
        CREATE INDEX "IX_Users_PrimaryUserId" ON "Users" ("PrimaryUserId");
        
        -- Add foreign key
        ALTER TABLE "Users" ADD CONSTRAINT "FK_Users_Users_PrimaryUserId" 
            FOREIGN KEY ("PrimaryUserId") REFERENCES "Users" ("Id") 
            ON DELETE RESTRICT;
        
        -- Record migration
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260420082000_AddUserAliases', '8.0.26')
        ON CONFLICT DO NOTHING;
        
        RAISE NOTICE 'Migration applied successfully';
    ELSE
        RAISE NOTICE 'Migration already applied';
    END IF;
END $$;
EOF
            
            echo "✅ Done!"
            exit 0
        fi
    done
done

echo "❌ Could not connect to database with any known user/database combination"
exit 1

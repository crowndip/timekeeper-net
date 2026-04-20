-- Manual migration script for adding alias support
-- Run this if auto-migration fails

-- Add PrimaryUserId column
ALTER TABLE "Users" ADD COLUMN "PrimaryUserId" uuid NULL;

-- Add foreign key constraint
ALTER TABLE "Users" ADD CONSTRAINT "FK_Users_Users_PrimaryUserId" 
    FOREIGN KEY ("PrimaryUserId") REFERENCES "Users" ("Id") 
    ON DELETE RESTRICT;

-- Add index for performance
CREATE INDEX "IX_Users_PrimaryUserId" ON "Users" ("PrimaryUserId");

-- Insert migration history record
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420082000_AddUserAliases', '8.0.26');

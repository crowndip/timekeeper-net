# User Alias Implementation Plan

**Version**: 1.0  
**Date**: 2026-04-20  
**Objective**: Merge multiple OS usernames under a single identity for unified time limit enforcement

---

## Overview

Allow administrators to designate multiple usernames (e.g., "joe", "john@hotmail.com") as aliases of a primary user (e.g., "john"). All time tracking and enforcement applies to the combined usage across all aliases, with no client-side changes required.

---

## Architecture Changes

### Database Schema

**Users Table Modification**:
```sql
ALTER TABLE Users ADD PrimaryUserId uniqueidentifier NULL;
ALTER TABLE Users ADD CONSTRAINT FK_Users_PrimaryUser 
    FOREIGN KEY (PrimaryUserId) REFERENCES Users(Id);
CREATE INDEX IX_Users_PrimaryUserId ON Users(PrimaryUserId);
```

**Terminology**:
- **Primary User**: User with `PrimaryUserId = NULL` (canonical identity)
- **Alias User**: User with `PrimaryUserId != NULL` (points to primary)

**Rules**:
1. Alias cannot point to another alias (must point to primary)
2. Primary user cannot be converted to alias if it has existing aliases
3. When user becomes alias, its TimeProfile becomes inactive
4. All usage tracking continues with original UserId for audit trail

---

## Core Logic Changes

### 1. User Resolution Service

**New Service**: `src/ParentalControl.WebService/Services/UserResolutionService.cs`

```csharp
public interface IUserResolutionService
{
    Task<User> ResolveToPrimaryAsync(Guid userId);
    Task<User> ResolveToPrimaryAsync(string username);
    Task<List<Guid>> GetAllUserIdsInGroupAsync(Guid primaryUserId);
    Task<bool> CanBecomeAliasAsync(Guid userId);
    Task<bool> CanBePrimaryAsync(Guid userId);
}
```

**Responsibilities**:
- Resolve any user to their primary user
- Get all userIds in an alias group (primary + all aliases)
- Validate alias operations

---

### 2. Time Calculation Service Changes

**File**: `src/ParentalControl.WebService/Services/TimeCalculationService.cs`

**Current**:
```csharp
var usedToday = await _context.TimeUsage
    .Where(u => u.UserId == userId && u.UsageDate == date)
    .SumAsync(u => u.MinutesUsed);
```

**New**:
```csharp
var primaryUser = await _userResolution.ResolveToPrimaryAsync(userId);
var allUserIds = await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id);

var usedToday = await _context.TimeUsage
    .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate == date)
    .SumAsync(u => u.MinutesUsed);
```

**Impact**: All time calculations now aggregate across alias group.

---

### 3. Client Controller Changes

**File**: `src/ParentalControl.WebService/Controllers/ClientController.cs`

**RegisterOrUpdate Endpoint**:

**Current Flow**:
1. Client reports username
2. Find or create user
3. Update last seen
4. Return time remaining

**New Flow**:
1. Client reports username
2. Find or create user
3. **Resolve to primary user**
4. Update last seen on **reported user** (audit trail)
5. Calculate time using **primary user**
6. Return time remaining

**Key Change**:
```csharp
var reportedUser = await GetOrCreateUser(request.Username);
var primaryUser = await _userResolution.ResolveToPrimaryAsync(reportedUser.Id);

// Update last seen on reported user (shows which login was used)
reportedUser.LastSeen = DateTime.UtcNow;

// Calculate time for primary user (enforces shared limits)
var timeRemaining = await _timeCalc.CalculateTimeRemainingAsync(
    primaryUser.Id, 
    DateOnly.FromDateTime(DateTime.UtcNow)
);
```

---

### 4. Users Controller Changes

**File**: `src/ParentalControl.WebService/Controllers/UsersController.cs`

**New Endpoints**:

```csharp
[HttpPost("{primaryUserId}/aliases/{aliasUserId}")]
[RequireAuth]
public async Task<IActionResult> AddAlias(Guid primaryUserId, Guid aliasUserId)

[HttpDelete("{aliasUserId}/unlink")]
[RequireAuth]
public async Task<IActionResult> RemoveAlias(Guid aliasUserId)

[HttpGet("{primaryUserId}/aliases")]
public async Task<ActionResult<List<UserAliasDto>>> GetAliases(Guid primaryUserId)

[HttpGet("{userId}/usage-breakdown")]
public async Task<ActionResult<UsageBreakdownDto>> GetUsageBreakdown(
    Guid userId, 
    DateOnly? startDate, 
    DateOnly? endDate
)
```

**Validation Rules**:
- Cannot make user an alias if it has active aliases pointing to it
- Cannot make user an alias if it has an active TimeProfile
- Cannot create circular references
- Primary user must exist and not be an alias itself

---

## UI Changes by Page

### Page 1: Users.razor

**Current Display**:
```
Username | Account Type | Last Seen | Actions
john     | Child        | 2 min ago | Edit | Delete
joe      | Child        | 5 min ago | Edit | Delete
```

**New Display**:
```
Username                    | Account Type | Last Seen | Actions
john                        | Child        | 2 min ago | Edit | Delete | Manage Aliases
├─ joe (alias)             | -            | 5 min ago | Unlink
└─ john@hotmail.com (alias)| -            | 1 day ago | Unlink
```

**Changes**:
1. Add "Manage Aliases" button for primary users
2. Show aliases indented under primary user
3. Alias rows show last seen but no account type
4. "Unlink" action for aliases
5. Filter: Option to show/hide aliases

**New Features**:
- Click "Manage Aliases" → Opens modal
- Modal shows:
  - Current aliases
  - Dropdown to select another user to make an alias
  - "Add Alias" button
  - Usage breakdown chart

---

### Page 2: Profiles.razor

**Impact**: Minimal

**Changes**:
1. When assigning profile to user, check if user is an alias
2. If alias, show warning: "This is an alias of {primaryUsername}. Profile will be assigned to {primaryUsername} instead."
3. Auto-redirect assignment to primary user

**Display**:
- Profiles page shows only primary users in assignment dropdown
- Aliases are hidden from profile assignment

---

### Page 3: Computers.razor

**Impact**: None

**Reasoning**: Computers are independent of user aliases. A computer can report usage for any username, and the resolution happens server-side.

---

### Page 4: Reports.razor

**Current**: Shows usage per user

**New Display**:

**Option A - Collapsed View (Default)**:
```
User    | Today | This Week | Total
john    | 45m   | 180m      | 1200m  [Expand]
```

**Option B - Expanded View**:
```
User                     | Today | This Week | Total
john (primary)           | 45m   | 180m      | 1200m  [Collapse]
├─ john (direct)        | 30m   | 120m      | 800m
├─ joe (alias)          | 15m   | 60m       | 350m
└─ john@hotmail.com     | 0m    | 0m        | 50m
```

**Features**:
1. Default shows aggregated usage for primary user
2. Click "Expand" to see per-alias breakdown
3. Chart shows combined usage over time
4. Tooltip on hover shows which alias contributed

**New Endpoint**: `GET /api/users/{userId}/usage-breakdown?start={date}&end={date}`

**Response**:
```json
{
  "primaryUser": {
    "id": "guid",
    "username": "john",
    "totalMinutes": 1200
  },
  "aliases": [
    {
      "id": "guid",
      "username": "joe",
      "totalMinutes": 350,
      "lastSeen": "2026-04-20T08:00:00Z"
    },
    {
      "id": "guid",
      "username": "john@hotmail.com",
      "totalMinutes": 50,
      "lastSeen": "2026-04-19T10:00:00Z"
    }
  ],
  "dailyBreakdown": [
    {
      "date": "2026-04-20",
      "totalMinutes": 45,
      "byUser": {
        "john": 30,
        "joe": 15,
        "john@hotmail.com": 0
      }
    }
  ]
}
```

---

### Page 5: Index.razor (Dashboard)

**Current**: Shows current status per user

**New Display**:
```
User    | Status      | Time Remaining | Last Seen
john    | Active      | 75 minutes     | 2 min ago (as joe)
```

**Changes**:
1. Show primary username
2. "Last Seen" indicates which alias was used: "(as joe)"
3. Time remaining is for the entire alias group

---

## Database Schema Migration Strategy

### Overview

The alias feature requires adding a single nullable column `PrimaryUserId` to the `Users` table. This section covers two scenarios:

1. **New Database**: Creating schema from scratch with alias support
2. **Existing Database**: Migrating existing production database

---

### Scenario 1: New Database (Fresh Installation)

**When**: First-time deployment, no existing data

**Process**:
1. Run all migrations including the new alias migration
2. Database created with `PrimaryUserId` column from the start
3. All users created as primary users by default (`PrimaryUserId = NULL`)

**Migration File**: `Migrations/YYYYMMDDHHMMSS_AddUserAliases.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddUserAliases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add PrimaryUserId column (nullable)
        migrationBuilder.AddColumn<Guid?>(
            name: "PrimaryUserId",
            table: "Users",
            type: "uniqueidentifier",
            nullable: true);

        // Create index for performance
        migrationBuilder.CreateIndex(
            name: "IX_Users_PrimaryUserId",
            table: "Users",
            column: "PrimaryUserId");

        // Add foreign key constraint
        migrationBuilder.AddForeignKey(
            name: "FK_Users_Users_PrimaryUserId",
            table: "Users",
            column: "PrimaryUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove foreign key
        migrationBuilder.DropForeignKey(
            name: "FK_Users_Users_PrimaryUserId",
            table: "Users");

        // Remove index
        migrationBuilder.DropIndex(
            name: "IX_Users_PrimaryUserId",
            table: "Users");

        // Remove column
        migrationBuilder.DropColumn(
            name: "PrimaryUserId",
            table: "Users");
    }
}
```

**Model Update**: `src/ParentalControl.WebService/Models/Entities.cs`

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public DateTime? LastSeen { get; set; }
    
    // NEW: Alias support
    public Guid? PrimaryUserId { get; set; }
    public User? PrimaryUser { get; set; }
    public ICollection<User> Aliases { get; set; } = new List<User>();
    
    // Existing properties...
}
```

**DbContext Configuration**: `src/ParentalControl.WebService/Data/AppDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Existing configurations...

    // NEW: Configure self-referencing relationship
    modelBuilder.Entity<User>()
        .HasOne(u => u.PrimaryUser)
        .WithMany(u => u.Aliases)
        .HasForeignKey(u => u.PrimaryUserId)
        .OnDelete(DeleteBehavior.Restrict);

    // NEW: Index for performance
    modelBuilder.Entity<User>()
        .HasIndex(u => u.PrimaryUserId);
}
```

**Result**:
- ✅ Clean schema with alias support from day one
- ✅ No data migration needed
- ✅ All users are primary users by default

---

### Scenario 2: Existing Database (Production Migration)

**When**: Upgrading existing production system with user data

**Challenges**:
1. Database already has users, profiles, usage data
2. Cannot lose existing data
3. Minimal downtime required
4. Must be reversible if issues occur

**Migration Steps**:

#### Step 1: Pre-Migration Validation

**Script**: `scripts/validate-pre-migration.sql`

```sql
-- Check current database state
SELECT 
    'Users' AS TableName,
    COUNT(*) AS RecordCount
FROM Users;

SELECT 
    'TimeUsage' AS TableName,
    COUNT(*) AS RecordCount
FROM TimeUsage;

SELECT 
    'TimeProfiles' AS TableName,
    COUNT(*) AS RecordCount
FROM TimeProfiles;

-- Check for potential issues
SELECT 
    Username,
    COUNT(*) AS DuplicateCount
FROM Users
GROUP BY Username
HAVING COUNT(*) > 1;

-- Expected: No duplicates (Username should be unique)
```

#### Step 2: Backup Database

**Before applying migration**:

```bash
# PostgreSQL backup
pg_dump -h localhost -U postgres -d parental_control > backup_pre_alias_$(date +%Y%m%d_%H%M%S).sql

# Or using Docker
docker exec parental-control-db pg_dump -U postgres parental_control > backup_pre_alias_$(date +%Y%m%d_%H%M%S).sql
```

**Verification**:
```bash
# Check backup file size
ls -lh backup_pre_alias_*.sql

# Verify backup is valid
pg_restore --list backup_pre_alias_*.sql | head -20
```

#### Step 3: Apply Migration

**Option A: Using EF Core Migrations (Recommended)**

```bash
# Generate migration
cd src/ParentalControl.WebService
dotnet ef migrations add AddUserAliases

# Review generated migration
cat Migrations/*_AddUserAliases.cs

# Apply migration
dotnet ef database update

# Verify
dotnet ef migrations list
```

**Option B: Manual SQL Script**

**Script**: `scripts/migrate-add-aliases.sql`

```sql
-- Start transaction for safety
BEGIN TRANSACTION;

-- Add PrimaryUserId column (nullable, default NULL)
ALTER TABLE "Users" 
ADD COLUMN "PrimaryUserId" uuid NULL;

-- Create index for performance
CREATE INDEX "IX_Users_PrimaryUserId" 
ON "Users" ("PrimaryUserId");

-- Add foreign key constraint
ALTER TABLE "Users"
ADD CONSTRAINT "FK_Users_Users_PrimaryUserId"
FOREIGN KEY ("PrimaryUserId")
REFERENCES "Users" ("Id")
ON DELETE RESTRICT;

-- Verify changes
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_name = 'Users' 
AND column_name = 'PrimaryUserId';

-- Check all existing users have NULL (are primary users)
SELECT 
    COUNT(*) AS TotalUsers,
    COUNT("PrimaryUserId") AS AliasUsers,
    COUNT(*) - COUNT("PrimaryUserId") AS PrimaryUsers
FROM "Users";

-- Expected: TotalUsers = PrimaryUsers, AliasUsers = 0

COMMIT;
-- If any errors, run: ROLLBACK;
```

**Apply Script**:
```bash
# PostgreSQL
psql -h localhost -U postgres -d parental_control -f scripts/migrate-add-aliases.sql

# Or using Docker
docker exec -i parental-control-db psql -U postgres parental_control < scripts/migrate-add-aliases.sql
```

#### Step 4: Post-Migration Validation

**Script**: `scripts/validate-post-migration.sql`

```sql
-- Verify column exists
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'Users' 
AND column_name = 'PrimaryUserId';

-- Expected: uuid, YES (nullable), NULL (default)

-- Verify index exists
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'Users'
AND indexname = 'IX_Users_PrimaryUserId';

-- Verify foreign key exists
SELECT
    conname AS constraint_name,
    contype AS constraint_type
FROM pg_constraint
WHERE conname = 'FK_Users_Users_PrimaryUserId';

-- Verify all existing users are primary users
SELECT 
    COUNT(*) AS TotalUsers,
    SUM(CASE WHEN "PrimaryUserId" IS NULL THEN 1 ELSE 0 END) AS PrimaryUsers,
    SUM(CASE WHEN "PrimaryUserId" IS NOT NULL THEN 1 ELSE 0 END) AS AliasUsers
FROM "Users";

-- Expected: TotalUsers = PrimaryUsers, AliasUsers = 0

-- Verify data integrity (no orphaned references)
SELECT 
    u1."Username" AS AliasUser,
    u2."Username" AS PrimaryUser
FROM "Users" u1
LEFT JOIN "Users" u2 ON u1."PrimaryUserId" = u2."Id"
WHERE u1."PrimaryUserId" IS NOT NULL
AND u2."Id" IS NULL;

-- Expected: 0 rows (no orphaned references)

-- Verify no circular references
WITH RECURSIVE alias_chain AS (
    SELECT 
        "Id",
        "Username",
        "PrimaryUserId",
        1 AS depth
    FROM "Users"
    WHERE "PrimaryUserId" IS NOT NULL
    
    UNION ALL
    
    SELECT 
        u."Id",
        u."Username",
        u."PrimaryUserId",
        ac.depth + 1
    FROM "Users" u
    INNER JOIN alias_chain ac ON u."Id" = ac."PrimaryUserId"
    WHERE ac.depth < 10
)
SELECT * FROM alias_chain WHERE depth > 1;

-- Expected: 0 rows (no chains, all aliases point directly to primary)
```

#### Step 5: Application Deployment

**Deployment Order** (Zero-Downtime Strategy):

1. **Deploy Database Migration** (Step 3)
   - Database now has `PrimaryUserId` column
   - All values are NULL (backward compatible)
   - Old application code still works (ignores new column)

2. **Deploy New Application Code**
   - New code recognizes `PrimaryUserId` column
   - Handles both NULL (primary) and non-NULL (alias) cases
   - Backward compatible with existing data

3. **Verify Application**
   - Check logs for errors
   - Test user registration
   - Test time calculation
   - Test all pages load correctly

4. **Enable Alias Feature**
   - Feature is now available in UI
   - Admins can start creating aliases

**Rollback Plan** (if issues occur):

```sql
-- Emergency rollback (removes alias feature)
BEGIN TRANSACTION;

-- Remove foreign key
ALTER TABLE "Users" 
DROP CONSTRAINT "FK_Users_Users_PrimaryUserId";

-- Remove index
DROP INDEX "IX_Users_PrimaryUserId";

-- Remove column
ALTER TABLE "Users" 
DROP COLUMN "PrimaryUserId";

COMMIT;
```

**Note**: Rollback is safe only if no aliases have been created. If aliases exist, rollback will lose that configuration (but not usage data).

---

### Data Integrity Checks

**Automated Validation Service**: `src/ParentalControl.WebService/Services/AliasValidationService.cs`

```csharp
public class AliasValidationService
{
    public async Task<ValidationResult> ValidateDatabaseIntegrity()
    {
        var issues = new List<string>();

        // Check 1: No circular references
        var circular = await CheckCircularReferences();
        if (circular.Any())
            issues.Add($"Circular references found: {string.Join(", ", circular)}");

        // Check 2: No orphaned aliases
        var orphaned = await CheckOrphanedAliases();
        if (orphaned.Any())
            issues.Add($"Orphaned aliases found: {string.Join(", ", orphaned)}");

        // Check 3: No aliases pointing to aliases
        var chained = await CheckChainedAliases();
        if (chained.Any())
            issues.Add($"Chained aliases found: {string.Join(", ", chained)}");

        // Check 4: No primary users with active profiles that are aliases
        var profileConflicts = await CheckProfileConflicts();
        if (profileConflicts.Any())
            issues.Add($"Profile conflicts found: {string.Join(", ", profileConflicts)}");

        return new ValidationResult 
        { 
            IsValid = !issues.Any(), 
            Issues = issues 
        };
    }
}
```

**Run on Startup**: `Program.cs`

```csharp
// After database migration, before accepting requests
using (var scope = app.Services.CreateScope())
{
    var validator = scope.ServiceProvider.GetRequiredService<AliasValidationService>();
    var result = await validator.ValidateDatabaseIntegrity();
    
    if (!result.IsValid)
    {
        logger.LogError("Database integrity check failed: {Issues}", 
            string.Join("; ", result.Issues));
        throw new InvalidOperationException("Database integrity check failed");
    }
    
    logger.LogInformation("Database integrity check passed");
}
```

---

### Migration Testing Strategy

**Test Environment Setup**:

1. **Create test database with production-like data**:

```sql
-- Create test database
CREATE DATABASE parental_control_test;

-- Copy production data (sanitized)
INSERT INTO parental_control_test."Users" 
SELECT * FROM parental_control."Users";

INSERT INTO parental_control_test."TimeUsage"
SELECT * FROM parental_control."TimeUsage";

-- etc.
```

2. **Apply migration to test database**:

```bash
# Test migration
dotnet ef database update --connection "Host=localhost;Database=parental_control_test;..."

# Verify
psql -d parental_control_test -c "SELECT * FROM \"Users\" LIMIT 5;"
```

3. **Run integration tests**:

```bash
cd tests/ParentalControl.WebService.Tests
dotnet test --filter "Category=Integration"
```

4. **Manual testing**:
   - Create test aliases
   - Verify time calculations
   - Check reports
   - Test unlinking

5. **Performance testing**:

```bash
# Load test with aliases
ab -n 1000 -c 10 http://localhost:8080/api/client/register
```

---

### Migration Checklist

**Pre-Migration**:
- [ ] Backup production database
- [ ] Verify backup is valid
- [ ] Run pre-migration validation script
- [ ] Test migration on staging environment
- [ ] Review migration SQL
- [ ] Schedule maintenance window (if needed)
- [ ] Notify users of potential downtime

**During Migration**:
- [ ] Stop application (or use zero-downtime strategy)
- [ ] Apply database migration
- [ ] Run post-migration validation script
- [ ] Verify all checks pass
- [ ] Deploy new application code
- [ ] Start application

**Post-Migration**:
- [ ] Verify application starts successfully
- [ ] Check application logs for errors
- [ ] Test user registration
- [ ] Test time calculation
- [ ] Test all UI pages
- [ ] Run automated integration tests
- [ ] Monitor for 24 hours

**Rollback Criteria**:
- Application fails to start
- Database validation fails
- Critical functionality broken
- Performance degradation > 50%

---

### Monitoring Post-Migration

**Key Metrics**:

```sql
-- Monitor alias usage
SELECT 
    COUNT(CASE WHEN "PrimaryUserId" IS NULL THEN 1 END) AS PrimaryUsers,
    COUNT(CASE WHEN "PrimaryUserId" IS NOT NULL THEN 1 END) AS AliasUsers,
    COUNT(*) AS TotalUsers
FROM "Users";

-- Monitor query performance
EXPLAIN ANALYZE
SELECT SUM("MinutesUsed")
FROM "TimeUsage"
WHERE "UserId" IN (
    SELECT "Id" FROM "Users" 
    WHERE "Id" = @primaryId OR "PrimaryUserId" = @primaryId
)
AND "UsageDate" = CURRENT_DATE;

-- Check for slow queries
SELECT 
    query,
    mean_exec_time,
    calls
FROM pg_stat_statements
WHERE query LIKE '%PrimaryUserId%'
ORDER BY mean_exec_time DESC
LIMIT 10;
```

**Alerts**:
- Query time > 100ms
- Failed alias operations
- Validation errors in logs

---

### Summary

**New Database**:
- ✅ Simple: Run migrations, done
- ✅ Clean schema from start
- ✅ No data migration needed

**Existing Database**:
- ✅ Safe: Backup → Migrate → Validate
- ✅ Reversible: Can rollback if needed
- ✅ Zero-downtime: Column addition is non-breaking
- ✅ Tested: Validation scripts ensure integrity

**Key Points**:
1. Migration adds one nullable column (backward compatible)
2. All existing users remain primary users (PrimaryUserId = NULL)
3. No data loss or modification
4. Feature is opt-in (admins create aliases manually)
5. Comprehensive validation ensures data integrity

---

## Virtual Testing Scenarios

### Scenario 1: Basic Alias Creation

**Setup**:
- User "john" exists (Id: A, PrimaryUserId: NULL)
- User "joe" exists (Id: B, PrimaryUserId: NULL)
- Both have separate usage history

**Action**: Make "joe" an alias of "john"

**Steps**:
1. Admin clicks "Manage Aliases" on john
2. Selects "joe" from dropdown
3. Clicks "Add Alias"

**Backend**:
```sql
UPDATE Users SET PrimaryUserId = 'A' WHERE Id = 'B'
```

**Result**:
- joe.PrimaryUserId = A
- joe's TimeProfile becomes inactive (if any)
- joe's usage history remains unchanged (UserId = B)
- Time calculations now aggregate john (A) + joe (B)

**Verification**:
```csharp
// Client reports as "joe"
var reportedUser = GetUser("joe"); // Id: B
var primaryUser = ResolveToPrimary(B); // Returns user A (john)

// Calculate time
var allIds = GetAllUserIds(A); // Returns [A, B]
var usage = TimeUsage.Where(u => allIds.Contains(u.UserId)).Sum();
// Returns combined usage of john + joe
```

**Expected Behavior**:
- ✅ Client logging in as "joe" gets john's time limits
- ✅ Usage from both logins counts toward shared limit
- ✅ Reports show combined usage
- ✅ Can still see individual breakdown

---

### Scenario 2: Multiple Aliases

**Setup**:
- john (A, primary)
- joe (B, alias of A)

**Action**: Add "john@hotmail.com" as alias

**Steps**:
1. Client reports username "john@hotmail.com"
2. Server auto-creates user (Id: C, PrimaryUserId: NULL)
3. Admin makes C an alias of A

**Backend**:
```sql
UPDATE Users SET PrimaryUserId = 'A' WHERE Id = 'C'
```

**Result**:
- allUserIds(A) = [A, B, C]
- All three usernames share same limits

**Verification**:
```csharp
// Day 1: john uses 50 minutes
TimeUsage.Add(new { UserId = A, Minutes = 50 });

// Day 1: joe uses 30 minutes  
TimeUsage.Add(new { UserId = B, Minutes = 30 });

// Day 1: john@hotmail.com uses 20 minutes
TimeUsage.Add(new { UserId = C, Minutes = 20 });

// Calculate remaining (limit: 120/day)
var used = GetUsage(A); // Returns 100 (50+30+20)
var remaining = 120 - 100; // 20 minutes
```

**Expected Behavior**:
- ✅ All three logins share 120 minute daily limit
- ✅ After 100 minutes combined, only 20 remain
- ✅ Enforcement applies to all three logins

---

### Scenario 3: Unlinking Alias

**Setup**:
- john (A, primary)
- joe (B, alias of A)
- Combined usage: 80 minutes today

**Action**: Unlink joe from john

**Steps**:
1. Admin clicks "Unlink" on joe
2. Confirms action

**Backend**:
```sql
UPDATE Users SET PrimaryUserId = NULL WHERE Id = 'B'
```

**Result**:
- joe becomes independent primary user again
- Historical usage remains (UserId = B)
- Future usage tracked separately

**Verification**:
```csharp
// Before unlink
var used = GetUsage(A); // 80 minutes (john + joe)

// After unlink
var usedJohn = GetUsage(A); // Only john's usage
var usedJoe = GetUsage(B); // Only joe's usage
// They are now independent
```

**Expected Behavior**:
- ✅ joe gets separate time limits again
- ✅ Historical data preserved
- ✅ No data loss

---

### Scenario 4: Profile Assignment

**Setup**:
- john (A, primary) - has "School Days" profile (120 min/day)
- joe (B, alias of A)

**Action**: Try to assign "Weekend" profile to joe

**Backend Check**:
```csharp
var user = GetUser(B); // joe
if (user.PrimaryUserId.HasValue) {
    var primary = GetUser(user.PrimaryUserId.Value);
    // Redirect assignment to primary
    primary.TimeProfile = weekendProfile;
}
```

**Result**:
- Profile assigned to john (A), not joe (B)
- UI shows warning: "joe is an alias of john. Profile assigned to john."

**Expected Behavior**:
- ✅ Cannot assign different profiles to aliases
- ✅ All aliases use primary user's profile
- ✅ Clear feedback to admin

---

### Scenario 5: Client Registration Flow

**Setup**:
- john (A, primary)
- joe (B, alias of A)
- john has 120 min/day limit, used 80 min today

**Action**: Client logs in as "joe"

**Flow**:
```csharp
// 1. Client reports
POST /api/client/register
{ "username": "joe", "computerName": "PC1" }

// 2. Server resolves
var reportedUser = GetOrCreateUser("joe"); // B
var primaryUser = ResolveToPrimary(B); // A (john)

// 3. Update last seen
reportedUser.LastSeen = Now; // joe shows as recently used

// 4. Calculate time
var allIds = GetAllUserIds(A); // [A, B]
var used = GetUsage(allIds); // 80 minutes
var remaining = 120 - 80; // 40 minutes

// 5. Response
return { 
    timeRemaining: 40,
    shouldEnforce: false,
    displayName: "john" // or "joe (as john)"?
}
```

**Expected Behavior**:
- ✅ joe gets 40 minutes (shared limit)
- ✅ joe's last seen updates (shows which login is active)
- ✅ Time enforcement applies correctly
- ✅ No client changes needed

---

### Scenario 6: Preventing Invalid Operations

**Test 1: Circular Reference**

**Setup**:
- john (A, primary)
- joe (B, alias of A)

**Action**: Try to make john an alias of joe

**Validation**:
```csharp
var joe = GetUser(B);
if (joe.PrimaryUserId.HasValue) {
    return BadRequest("Cannot make user an alias of an alias");
}
```

**Result**: ❌ Operation rejected

---

**Test 2: Primary with Aliases**

**Setup**:
- john (A, primary)
- joe (B, alias of A)

**Action**: Try to make john an alias of someone else

**Validation**:
```csharp
var aliases = GetAliases(A);
if (aliases.Any()) {
    return BadRequest("Cannot convert user with aliases to an alias");
}
```

**Result**: ❌ Operation rejected

---

**Test 3: Alias with Active Profile**

**Setup**:
- joe (B, primary) - has active profile

**Action**: Try to make joe an alias

**Validation**:
```csharp
var profile = GetActiveProfile(B);
if (profile != null) {
    return BadRequest("User has active profile. Deactivate profile first.");
}
```

**Result**: ❌ Operation rejected (or auto-deactivate with warning)

---

## Edge Cases & Solutions

### Edge Case 1: Auto-Created Users

**Scenario**: Client reports username "newuser" that doesn't exist

**Current Behavior**: Auto-create user

**New Behavior**: 
- Auto-create as primary user (PrimaryUserId = NULL)
- Admin can later convert to alias if needed

**No change required** - works as expected.

---

### Edge Case 2: Deleted Primary User

**Scenario**: Admin deletes primary user that has aliases

**Solution**: 
```csharp
// Before delete, check for aliases
var aliases = GetAliases(userId);
if (aliases.Any()) {
    return BadRequest("Cannot delete user with aliases. Unlink aliases first.");
}
```

**Alternative**: Cascade - promote first alias to primary, update others.

---

### Edge Case 3: Time Adjustments

**Scenario**: Admin adds +30 minutes to "joe" (alias)

**Current**: Adjustment added to joe's record

**New Behavior**:
```csharp
var primaryUser = ResolveToPrimary(userId);
// Add adjustment to primary user instead
TimeAdjustment.Add(new { 
    UserId = primaryUser.Id, 
    Minutes = 30,
    Reason = "Added via alias: joe"
});
```

**Result**: Adjustment applies to entire alias group.

---

### Edge Case 4: Reports Date Range

**Scenario**: View usage for last 30 days, joe became alias 15 days ago

**Expected**: 
- Days 1-15: joe shows as independent user
- Days 16-30: joe's usage included in john's total

**Solution**: Reports always show current alias structure, but historical data remains accurate.

---

## Performance Considerations

### Query Optimization

**Before** (single user):
```sql
SELECT SUM(MinutesUsed) 
FROM TimeUsage 
WHERE UserId = @userId AND UsageDate = @date
```

**After** (alias group):
```sql
SELECT SUM(MinutesUsed) 
FROM TimeUsage 
WHERE UserId IN (@id1, @id2, @id3) AND UsageDate = @date
```

**Impact**: Minimal - IN clause with 2-5 IDs is fast with index.

**Index**: `IX_TimeUsage_UserId_UsageDate` (already exists)

---

### Caching Strategy

**Cache alias groups**:
```csharp
// Cache key: "aliases:{primaryUserId}"
// Value: List<Guid> of all user IDs in group
// TTL: 5 minutes (or invalidate on alias change)
```

**Benefit**: Avoid repeated database lookups for alias resolution.

---

## Testing Strategy

### Unit Tests

#### Test File 1: UserResolutionServiceTests.cs

**Location**: `tests/ParentalControl.WebService.Tests/UserResolutionServiceTests.cs`

**Purpose**: Test user resolution logic in isolation

**Test Cases**:

```csharp
public class UserResolutionServiceTests
{
    [Fact]
    public async Task ResolveToPrimary_PrimaryUser_ReturnsSelf()
    {
        // Arrange: User with PrimaryUserId = NULL
        // Act: Resolve to primary
        // Assert: Returns same user
    }

    [Fact]
    public async Task ResolveToPrimary_AliasUser_ReturnsPrimary()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: Resolve B to primary
        // Assert: Returns A
    }

    [Fact]
    public async Task ResolveToPrimary_ByUsername_FindsAndResolves()
    {
        // Arrange: User "joe" is alias of "john"
        // Act: Resolve "joe" by username
        // Assert: Returns john's User object
    }

    [Fact]
    public async Task GetAllUserIds_PrimaryWithAliases_ReturnsAll()
    {
        // Arrange: User A (primary), Users B, C (aliases of A)
        // Act: GetAllUserIds(A)
        // Assert: Returns [A, B, C]
    }

    [Fact]
    public async Task GetAllUserIds_PrimaryWithoutAliases_ReturnsSelf()
    {
        // Arrange: User A (primary, no aliases)
        // Act: GetAllUserIds(A)
        // Assert: Returns [A]
    }

    [Fact]
    public async Task GetAllUserIds_AliasUser_ReturnsGroupIncludingPrimary()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: GetAllUserIds(B)
        // Assert: Returns [A, B] (resolves to primary first)
    }

    [Fact]
    public async Task CanBecomeAlias_UserWithAliases_ReturnsFalse()
    {
        // Arrange: User A has aliases B, C
        // Act: CanBecomeAlias(A)
        // Assert: False (cannot convert primary with aliases to alias)
    }

    [Fact]
    public async Task CanBecomeAlias_UserWithActiveProfile_ReturnsFalse()
    {
        // Arrange: User A has active TimeProfile
        // Act: CanBecomeAlias(A)
        // Assert: False (must deactivate profile first)
    }

    [Fact]
    public async Task CanBecomeAlias_IndependentUser_ReturnsTrue()
    {
        // Arrange: User A (no aliases, no active profile)
        // Act: CanBecomeAlias(A)
        // Assert: True
    }

    [Fact]
    public async Task CanBePrimary_AliasUser_ReturnsFalse()
    {
        // Arrange: User B is alias of A
        // Act: CanBePrimary(B)
        // Assert: False (alias cannot be primary)
    }

    [Fact]
    public async Task GetPrimaryUser_NonExistentUser_ReturnsNull()
    {
        // Arrange: Invalid userId
        // Act: ResolveToPrimary(invalidId)
        // Assert: Returns null or throws
    }
}
```

**Total**: 11 test cases

---

#### Test File 2: TimeCalculationServiceTests (Updates)

**Location**: `tests/ParentalControl.WebService.Tests/TimeCalculationServiceTests.cs`

**Purpose**: Verify time calculations work correctly with aliases

**New Test Cases**:

```csharp
public class TimeCalculationServiceTests
{
    // Existing tests remain...

    [Fact]
    public async Task CalculateTimeRemaining_AliasUser_AggregatesUsage()
    {
        // Arrange: 
        //   - User A (primary), User B (alias of A)
        //   - Profile: 120 min/day
        //   - A used 50 min, B used 30 min
        // Act: CalculateTimeRemaining(B, today)
        // Assert: Returns 40 (120 - 80)
    }

    [Fact]
    public async Task CalculateTimeRemaining_MultipleAliases_AggregatesAll()
    {
        // Arrange:
        //   - User A (primary), Users B, C, D (aliases)
        //   - Profile: 120 min/day
        //   - A: 20 min, B: 30 min, C: 40 min, D: 10 min
        // Act: CalculateTimeRemaining(A, today)
        // Assert: Returns 20 (120 - 100)
    }

    [Fact]
    public async Task CalculateTimeRemaining_WeeklyLimit_AggregatesAliases()
    {
        // Arrange:
        //   - User A (primary), User B (alias)
        //   - Profile: 120 min/day, 300 min/week
        //   - Monday: A used 100, B used 50 (total 150)
        //   - Tuesday: A used 80, B used 70 (total 150)
        //   - Wednesday (today): no usage yet
        // Act: CalculateTimeRemaining(A, Wednesday)
        // Assert: Returns 0 (weekly limit reached: 300 - 300 = 0)
    }

    [Fact]
    public async Task CalculateTimeRemaining_AdjustmentsOnAlias_ApplyToPrimary()
    {
        // Arrange:
        //   - User A (primary), User B (alias)
        //   - Profile: 120 min/day
        //   - A used 80 min
        //   - Adjustment: +30 min added to B
        // Act: CalculateTimeRemaining(A, today)
        // Assert: Returns 70 (120 - 80 + 30)
    }

    [Fact]
    public async Task CalculateTimeRemaining_UnlinkedAlias_IndependentCalculation()
    {
        // Arrange:
        //   - User A (primary), User B (was alias, now unlinked)
        //   - Profile A: 120 min/day, Profile B: 60 min/day
        //   - A used 50 min, B used 30 min
        // Act: CalculateTimeRemaining(B, today)
        // Assert: Returns 30 (60 - 30, independent from A)
    }

    [Fact]
    public async Task IsWithinAllowedHours_AliasUser_UsesPrimaryProfile()
    {
        // Arrange:
        //   - User A (primary) with allowed hours 9-17
        //   - User B (alias of A)
        //   - Current time: 10:00
        // Act: IsWithinAllowedHours(B, 10:00)
        // Assert: True (uses A's profile)
    }

    [Fact]
    public async Task ShouldEnforce_AliasExceedsLimit_EnforcesForAll()
    {
        // Arrange:
        //   - User A (primary), User B (alias)
        //   - Profile: 120 min/day
        //   - Combined usage: 125 min
        // Act: ShouldEnforce(B, -5)
        // Assert: True (enforcement applies to entire group)
    }
}
```

**Total**: 7 new test cases

---

#### Test File 3: UsersControllerTests (Updates)

**Location**: `tests/ParentalControl.WebService.Tests/UsersControllerTests.cs`

**Purpose**: Test alias management API endpoints

**New Test Cases**:

```csharp
public class UsersControllerTests
{
    // Existing tests remain...

    [Fact]
    public async Task AddAlias_ValidUsers_CreatesAlias()
    {
        // Arrange: User A (primary), User B (independent)
        // Act: POST /api/users/A/aliases/B
        // Assert: B.PrimaryUserId = A, returns 200 OK
    }

    [Fact]
    public async Task AddAlias_AliasAsTarget_ReturnsBadRequest()
    {
        // Arrange: User A (primary), User B (alias of A), User C
        // Act: POST /api/users/B/aliases/C (trying to make B primary)
        // Assert: 400 Bad Request
    }

    [Fact]
    public async Task AddAlias_UserWithAliases_ReturnsBadRequest()
    {
        // Arrange: User A (primary with aliases), User B
        // Act: POST /api/users/C/aliases/A (trying to make A an alias)
        // Assert: 400 Bad Request
    }

    [Fact]
    public async Task AddAlias_UserWithActiveProfile_ReturnsBadRequest()
    {
        // Arrange: User A, User B (has active profile)
        // Act: POST /api/users/A/aliases/B
        // Assert: 400 Bad Request
    }

    [Fact]
    public async Task AddAlias_CircularReference_ReturnsBadRequest()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: POST /api/users/B/aliases/A (circular)
        // Assert: 400 Bad Request
    }

    [Fact]
    public async Task RemoveAlias_ValidAlias_Unlinks()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: DELETE /api/users/B/unlink
        // Assert: B.PrimaryUserId = NULL, returns 200 OK
    }

    [Fact]
    public async Task RemoveAlias_PrimaryUser_ReturnsBadRequest()
    {
        // Arrange: User A (primary, not an alias)
        // Act: DELETE /api/users/A/unlink
        // Assert: 400 Bad Request
    }

    [Fact]
    public async Task GetAliases_PrimaryWithAliases_ReturnsAll()
    {
        // Arrange: User A (primary), Users B, C (aliases)
        // Act: GET /api/users/A/aliases
        // Assert: Returns [B, C]
    }

    [Fact]
    public async Task GetAliases_PrimaryWithoutAliases_ReturnsEmpty()
    {
        // Arrange: User A (primary, no aliases)
        // Act: GET /api/users/A/aliases
        // Assert: Returns []
    }

    [Fact]
    public async Task GetUsageBreakdown_PrimaryWithAliases_ReturnsBreakdown()
    {
        // Arrange: User A (primary), User B (alias)
        //   - A used 100 min, B used 50 min
        // Act: GET /api/users/A/usage-breakdown?start=today&end=today
        // Assert: Returns { total: 150, byUser: { A: 100, B: 50 } }
    }

    [Fact]
    public async Task UpdateUser_AliasUser_UpdatesAlias()
    {
        // Arrange: User A (primary), User B (alias)
        // Act: PUT /api/users/B { accountType: Parent }
        // Assert: B.AccountType = Parent (can update alias properties)
    }

    [Fact]
    public async Task DeleteUser_PrimaryWithAliases_ReturnsBadRequest()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: DELETE /api/users/A
        // Assert: 400 Bad Request (must unlink aliases first)
    }

    [Fact]
    public async Task DeleteUser_AliasUser_Deletes()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: DELETE /api/users/B
        // Assert: B deleted, A unaffected
    }
}
```

**Total**: 13 new test cases

---

#### Test File 4: ClientControllerTests (Updates)

**Location**: `tests/ParentalControl.WebService.Tests/ClientControllerTests.cs`

**Purpose**: Test client registration with alias resolution

**New Test Cases**:

```csharp
public class ClientControllerTests
{
    // Existing tests remain...

    [Fact]
    public async Task RegisterOrUpdate_AliasUsername_ResolvesToPrimary()
    {
        // Arrange: User A (primary), User B (alias of A)
        //   - Profile: 120 min/day, A used 80 min
        // Act: POST /api/client/register { username: "B" }
        // Assert: Returns timeRemaining: 40 (uses A's limits)
    }

    [Fact]
    public async Task RegisterOrUpdate_AliasUsername_UpdatesAliasLastSeen()
    {
        // Arrange: User A (primary), User B (alias)
        // Act: POST /api/client/register { username: "B" }
        // Assert: B.LastSeen updated (shows which login was used)
    }

    [Fact]
    public async Task RegisterOrUpdate_NewUsername_CreatesAsPrimary()
    {
        // Arrange: No existing user
        // Act: POST /api/client/register { username: "newuser" }
        // Assert: User created with PrimaryUserId = NULL
    }

    [Fact]
    public async Task ReportUsage_AliasUser_RecordsWithAliasId()
    {
        // Arrange: User A (primary), User B (alias)
        // Act: POST /api/client/usage { username: "B", minutes: 30 }
        // Assert: TimeUsage record created with UserId = B (audit trail)
    }

    [Fact]
    public async Task ReportUsage_AliasUser_EnforcesSharedLimit()
    {
        // Arrange: User A (primary), User B (alias)
        //   - Profile: 120 min/day
        //   - A used 100 min, B reports 25 min
        // Act: POST /api/client/usage { username: "B", minutes: 25 }
        // Assert: Returns shouldEnforce: true (125 > 120)
    }
}
```

**Total**: 5 new test cases

---

#### Test File 5: ProfilesControllerTests (Updates)

**Location**: `tests/ParentalControl.WebService.Tests/ProfilesControllerTests.cs`

**Purpose**: Test profile assignment with aliases

**New Test Cases**:

```csharp
public class ProfilesControllerTests
{
    // Existing tests remain...

    [Fact]
    public async Task ActivateProfile_AliasUser_ActivatesForPrimary()
    {
        // Arrange: User A (primary), User B (alias), Profile P
        // Act: POST /api/profiles/P/activate { userId: B }
        // Assert: Profile activated for A (not B)
    }

    [Fact]
    public async Task GetActiveProfile_AliasUser_ReturnsPrimaryProfile()
    {
        // Arrange: User A (primary with active profile), User B (alias)
        // Act: GET /api/profiles/active?userId=B
        // Assert: Returns A's active profile
    }

    [Fact]
    public async Task CreateProfile_ForAlias_CreatesForPrimary()
    {
        // Arrange: User A (primary), User B (alias)
        // Act: POST /api/profiles { userId: B, ... }
        // Assert: Profile created with UserId = A
    }
}
```

**Total**: 3 new test cases

---

### Integration Tests

#### Test File 6: AliasIntegrationTests.cs

**Location**: `tests/ParentalControl.WebService.Tests/AliasIntegrationTests.cs`

**Purpose**: End-to-end testing of alias functionality

**Test Cases**:

```csharp
public class AliasIntegrationTests
{
    [Fact]
    public async Task FullFlow_CreateAlias_TimeAggregation_Unlink()
    {
        // Arrange: Create users A and B
        // Act 1: Make B alias of A
        // Assert 1: B.PrimaryUserId = A
        
        // Act 2: Report usage for A (50 min) and B (30 min)
        // Assert 2: Combined usage = 80 min
        
        // Act 3: Calculate time remaining (limit: 120)
        // Assert 3: Returns 40 min
        
        // Act 4: Unlink B from A
        // Assert 4: B.PrimaryUserId = NULL
        
        // Act 5: Calculate time for B
        // Assert 5: Returns B's independent limit
    }

    [Fact]
    public async Task MultipleAliases_ConcurrentUsage_CorrectAggregation()
    {
        // Arrange: User A (primary), Users B, C, D (aliases)
        // Act: Report usage concurrently from all 4 users
        // Assert: Total usage correctly aggregated, no race conditions
    }

    [Fact]
    public async Task AliasEnforcement_ExceedsLimit_AllAliasesEnforced()
    {
        // Arrange: User A (primary), User B (alias)
        //   - Profile: 120 min/day
        //   - A used 100 min
        // Act: B reports 25 min (total: 125)
        // Assert: Both A and B get shouldEnforce: true
    }

    [Fact]
    public async Task ProfileChange_AffectsAllAliases()
    {
        // Arrange: User A (primary), User B (alias)
        //   - Profile 1: 120 min/day (active)
        // Act: Switch to Profile 2: 60 min/day
        // Assert: Both A and B now have 60 min/day limit
    }

    [Fact]
    public async Task WeeklyLimit_AcrossAliases_CorrectCalculation()
    {
        // Arrange: User A (primary), User B (alias)
        //   - Profile: 120 min/day, 300 min/week
        // Act: 
        //   - Monday: A uses 100, B uses 50 (total: 150)
        //   - Tuesday: A uses 80, B uses 70 (total: 150)
        //   - Wednesday: Check remaining
        // Assert: Weekly remaining = 0 (300 - 300)
    }

    [Fact]
    public async Task AllowedHours_AliasUser_UsesPrimarySchedule()
    {
        // Arrange: User A (primary) with allowed hours 9-17
        //   - User B (alias of A)
        // Act: Check at 10:00 for B
        // Assert: Within allowed hours (uses A's schedule)
    }

    [Fact]
    public async Task UsageReports_ShowBreakdown_ByAlias()
    {
        // Arrange: User A (primary), Users B, C (aliases)
        //   - A: 50 min, B: 30 min, C: 20 min
        // Act: GET /api/users/A/usage-breakdown
        // Assert: Returns breakdown showing each user's contribution
    }

    [Fact]
    public async Task DatabaseMigration_ExistingUsers_RemainPrimary()
    {
        // Arrange: Database with existing users (no PrimaryUserId)
        // Act: Run migration, add PrimaryUserId column
        // Assert: All existing users have PrimaryUserId = NULL
    }

    [Fact]
    public async Task AliasValidation_CircularReference_Prevented()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: Try to make A an alias of B
        // Assert: Validation fails, operation rejected
    }

    [Fact]
    public async Task AliasValidation_ChainedAliases_Prevented()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: Try to make User C an alias of B (not A)
        // Assert: Validation fails, must point to primary
    }

    [Fact]
    public async Task DeletePrimaryWithAliases_Prevented()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: Try to delete A
        // Assert: Operation rejected, must unlink aliases first
    }

    [Fact]
    public async Task DeleteAlias_PrimaryUnaffected()
    {
        // Arrange: User A (primary), User B (alias of A)
        // Act: Delete B
        // Assert: B deleted, A remains, A's usage history intact
    }

    [Fact]
    public async Task HistoricalData_PreservedAfterAliasChange()
    {
        // Arrange: User A (100 min usage), User B (50 min usage)
        // Act: Make B alias of A
        // Assert: Historical records unchanged (UserId preserved)
        //   - TimeUsage still shows A: 100, B: 50
        //   - Combined calculation: 150
    }
}
```

**Total**: 13 test cases

---

### Performance Tests

#### Test File 7: AliasPerformanceTests.cs

**Location**: `tests/ParentalControl.WebService.Tests/AliasPerformanceTests.cs`

**Purpose**: Ensure alias feature doesn't degrade performance

**Test Cases**:

```csharp
public class AliasPerformanceTests
{
    [Fact]
    public async Task ResolveToPrimary_1000Users_CompletesUnder10ms()
    {
        // Arrange: 1000 users, 200 are aliases
        // Act: Resolve 100 random users to primary
        // Assert: Average time < 10ms per resolution
    }

    [Fact]
    public async Task CalculateTime_10Aliases_CompletesUnder50ms()
    {
        // Arrange: User A with 10 aliases, 30 days of usage data
        // Act: Calculate time remaining
        // Assert: Query completes < 50ms
    }

    [Fact]
    public async Task GetUsageBreakdown_LargeDataset_CompletesUnder200ms()
    {
        // Arrange: User A with 5 aliases, 90 days of usage data
        // Act: GET /api/users/A/usage-breakdown?start=90daysAgo&end=today
        // Assert: Response time < 200ms
    }

    [Fact]
    public async Task ConcurrentClientRequests_100Aliases_NoDeadlocks()
    {
        // Arrange: 100 users (50 primary, 50 aliases)
        // Act: Simulate 100 concurrent client registrations
        // Assert: All complete successfully, no deadlocks
    }
}
```

**Total**: 4 test cases

---

### UI Tests (Manual Test Plan)

**Location**: `docs/ALIAS_TESTING_GUIDE.md`

**Purpose**: Manual testing checklist for UI functionality

**Test Scenarios**:

1. **Users Page - Display**
   - [ ] Primary users show "Manage Aliases" button
   - [ ] Aliases appear indented under primary user
   - [ ] Alias rows show last seen time
   - [ ] "Unlink" button appears for aliases
   - [ ] Filter to show/hide aliases works

2. **Users Page - Alias Management Modal**
   - [ ] Modal opens when clicking "Manage Aliases"
   - [ ] Shows current aliases list
   - [ ] Dropdown shows available users to make aliases
   - [ ] "Add Alias" button creates alias
   - [ ] Success message appears
   - [ ] Modal closes and list refreshes

3. **Users Page - Validation**
   - [ ] Cannot make user with aliases an alias
   - [ ] Cannot make user with active profile an alias
   - [ ] Cannot create circular reference
   - [ ] Error messages are clear

4. **Reports Page - Collapsed View**
   - [ ] Shows aggregated usage for primary user
   - [ ] "Expand" button visible
   - [ ] Chart shows combined usage

5. **Reports Page - Expanded View**
   - [ ] Shows per-alias breakdown
   - [ ] Individual usage numbers correct
   - [ ] "Collapse" button works
   - [ ] Tooltip shows alias contribution

6. **Dashboard - Last Seen**
   - [ ] Shows "(as aliasname)" when alias was used
   - [ ] Updates in real-time
   - [ ] Time remaining reflects combined usage

7. **Profiles Page**
   - [ ] Warning appears when assigning to alias
   - [ ] Profile assigned to primary user
   - [ ] Aliases hidden from dropdown

---

### Test Coverage Summary

**Total New Tests**: 56 automated test cases

**Breakdown by Category**:
- Unit Tests: 39 test cases
  - UserResolutionService: 11
  - TimeCalculationService: 7
  - UsersController: 13
  - ClientController: 5
  - ProfilesController: 3
- Integration Tests: 13 test cases
- Performance Tests: 4 test cases

**Existing Tests to Update**: ~10 test cases
- Ensure existing tests still pass with nullable PrimaryUserId
- Update mocks to include alias resolution

**Manual UI Tests**: 7 test scenarios with 25+ checkpoints

**Estimated Testing Time**:
- Writing tests: 3-4 days
- Running automated tests: ~5 minutes
- Manual UI testing: 1-2 hours per release

**Coverage Goals**:
- Line coverage: > 90% for new code
- Branch coverage: > 85% for new code
- Integration coverage: All critical paths tested

---

### Test Execution Strategy

**Development Phase**:
```bash
# Run unit tests frequently
dotnet test --filter "Category=Unit"

# Run integration tests before commit
dotnet test --filter "Category=Integration"

# Run all tests before PR
dotnet test
```

**CI/CD Pipeline**:
1. Unit tests (fast feedback)
2. Integration tests (if unit tests pass)
3. Performance tests (if integration tests pass)
4. Manual UI testing (staging environment)

**Regression Testing**:
- Run full test suite on every release
- Monitor for performance degradation
- Track test execution time trends

---

## Rollout Plan

### Phase 1: Backend Foundation (Week 1)
- [ ] Add PrimaryUserId column (migration)
- [ ] Create UserResolutionService
- [ ] Update TimeCalculationService
- [ ] Update ClientController
- [ ] Add unit tests

### Phase 2: API Endpoints (Week 1)
- [ ] Add alias management endpoints
- [ ] Add usage breakdown endpoint
- [ ] Add validation logic
- [ ] Add integration tests

### Phase 3: UI Changes (Week 2)
- [ ] Update Users.razor (alias display)
- [ ] Add alias management modal
- [ ] Update Reports.razor (breakdown view)
- [ ] Update Index.razor (last seen indicator)
- [ ] Update Profiles.razor (alias warning)

### Phase 4: Testing & Documentation (Week 2)
- [ ] End-to-end testing
- [ ] Update README with alias feature
- [ ] Create admin guide for alias management
- [ ] Performance testing

### Phase 5: Deployment (Week 3)
- [ ] Deploy to staging
- [ ] User acceptance testing
- [ ] Deploy to production
- [ ] Monitor for issues

---

## Success Criteria

✅ **Functional**:
- Admin can create/remove aliases via UI
- Time limits apply to combined usage across aliases
- Historical data preserved and viewable
- No client changes required

✅ **Performance**:
- No noticeable slowdown in time calculations
- Alias resolution < 10ms
- Reports load in < 2 seconds

✅ **Usability**:
- Clear visual indication of alias relationships
- Easy to see which logins are actually used
- Simple workflow to merge/unmerge users

✅ **Robustness**:
- No data loss on alias operations
- Prevents invalid configurations
- Handles edge cases gracefully

---

## Open Questions

1. **Display Name**: Should client see "john" or "joe (as john)" when logged in as alias?
   - **Recommendation**: Keep original username for transparency YES

2. **Profile Inheritance**: When making user an alias, auto-deactivate their profile or require manual deactivation?
   - **Recommendation**: Require manual deactivation (safer) - Automatic

3. **Bulk Operations**: Support making multiple users aliases at once?
   - **Recommendation**: Phase 2 feature - NO

4. **API Response**: Should RegisterOrUpdate return primary username or reported username?
   - **Recommendation**: Return both for client logging - reported username

---

## Conclusion

This plan provides a robust, backward-compatible solution for user aliasing with:
- Minimal database changes (one column)
- Clear separation of concerns (UserResolutionService)
- Preserved audit trail (original UserId in TimeUsage)
- Flexible UI for management and reporting
- Comprehensive validation and error handling

The virtual testing scenarios demonstrate that the approach handles all common use cases and edge cases correctly.

**Estimated Effort**: 2-3 weeks for full implementation and testing.

# Automatic User Creation Feature

## Overview

The server now automatically creates user accounts when clients report usage for unknown users. This enables a streamlined workflow where parents can set up limited accounts on client computers first, and then configure them in the web interface.

## Workflow

### 1. Parent Sets Up Client Computer
- Parent creates limited user accounts on the client computer (Linux/Windows)
- Installs and starts the parental control client
- Client automatically registers the computer with the server

### 2. Client Reports Usage
- When a user logs in, the client detects the username
- Client sends usage reports with `UserId` (generated from username) and `Username`
- Server automatically creates user record if it doesn't exist
- New users are created with `AccountType = Unassigned`

### 3. Parent Configures Users
- Parent logs into web interface at `/users`
- Sees all auto-created users with red "Unassigned" badge
- Edits each user to set:
  - Account Type (Child/Parent/Technical)
  - Full Name
  - Email (optional)
- Creates time profiles for Child accounts

## Technical Implementation

### Database Changes
- Added `Unassigned = -1` to `AccountType` enum
- Unassigned users have no time limits (unlimited until configured)

### API Changes
- `UsageReportRequest` now includes `Username` field
- `SessionStartRequest` now includes `Username` field
- `ClientController` has new `EnsureUserExistsAsync()` method

### Client Changes
- `UsageRecord` now includes `Username` field
- `LocalCache.IncrementUsageAsync()` accepts `username` parameter
- Both Linux and Windows clients updated

### Server Logic
```csharp
private async Task EnsureUserExistsAsync(Guid userId, string username)
{
    var exists = await _context.Users.AnyAsync(u => u.Id == userId);
    if (!exists)
    {
        var user = new User
        {
            Id = userId,
            Username = username,
            AccountType = AccountType.Unassigned,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }
}
```

## User Interface

### Users Page
- Unassigned users shown with red badge
- Account type dropdown includes "Unassigned (Needs configuration)"
- Parents can edit and assign proper account type

### Badge Colors
- 🔴 Red: Unassigned (needs configuration)
- 🟠 Orange: Child (supervised)
- 🟢 Green: Parent (no limits)
- ⚪ Gray: Technical (system accounts)

## Security Considerations

- Users are created as `Unassigned` by default (no enforcement)
- Parents must explicitly configure each user
- Prevents unauthorized time limit bypass
- Maintains parental control over account types

## Migration Path

Existing deployments:
1. Update server to v1.3.0+
2. Update clients to v1.3.0+
3. Existing users remain unchanged
4. New users auto-created as Unassigned

## Example Scenario

**Before:**
1. Parent creates user "john" on Linux PC
2. Parent logs into web interface
3. Parent manually creates "john" user
4. Parent creates time profile
5. Client starts tracking

**After:**
1. Parent creates user "john" on Linux PC
2. Client starts and reports usage for "john"
3. Server auto-creates "john" as Unassigned
4. Parent logs into web interface
5. Parent sees "john" already listed (red badge)
6. Parent edits "john" → sets as Child
7. Parent creates time profile
8. Enforcement begins

## Benefits

- ✅ Faster setup (no manual user creation)
- ✅ No missed tracking (users created immediately)
- ✅ Clear visibility (Unassigned badge shows what needs configuration)
- ✅ Flexible workflow (configure users at parent's convenience)
- ✅ Cross-platform (works on Linux and Windows)


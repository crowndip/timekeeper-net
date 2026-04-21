# Account Classification

## Account Types

The system now supports three types of accounts:

### 1. Child (Supervised)
- **Purpose**: Children with time limits
- **Limits**: Yes - daily/weekly time limits enforced
- **Tracking**: All usage tracked
- **Enforcement**: Logout/lock when limits reached
- **Default**: New accounts default to Child type

### 2. Parent (Unsupervised)
- **Purpose**: Parents/guardians without limits
- **Limits**: No - unlimited computer time
- **Tracking**: Optional (not enforced)
- **Enforcement**: None
- **Use Case**: Adult family members

### 3. Technical (System)
- **Purpose**: System/service accounts
- **Limits**: No - unlimited access
- **Tracking**: No
- **Enforcement**: None
- **Use Case**: System services, backup accounts, admin accounts

## Database Schema

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    username VARCHAR(64) NOT NULL UNIQUE,
    full_name VARCHAR(255),
    email VARCHAR(255),
    account_type VARCHAR(20) NOT NULL DEFAULT 'Child',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT valid_account_type CHECK (account_type IN ('Child', 'Parent', 'Technical'))
);
```

## API Usage

### Create User

**Child Account**:
```json
POST /api/admin/users
{
  "username": "johnny",
  "fullName": "Johnny Smith",
  "email": "johnny@example.com",
  "accountType": "Child"
}
```

**Parent Account**:
```json
POST /api/admin/users
{
  "username": "mom",
  "fullName": "Jane Smith",
  "email": "jane@example.com",
  "accountType": "Parent"
}
```

**Technical Account**:
```json
POST /api/admin/users
{
  "username": "backup",
  "fullName": "Backup Service",
  "accountType": "Technical"
}
```

## Client Behavior

### Configuration Sync

Only **Child** accounts are sent to clients:

```csharp
var users = await _context.Users
    .Where(u => u.IsActive && u.AccountType == AccountType.Child)
    .ToListAsync();
```

### Time Tracking

- **Child**: All usage tracked and enforced
- **Parent**: Not tracked by client
- **Technical**: Not tracked by client

### Enforcement

Only Child accounts are subject to:
- Time limits
- Allowed hours
- Warnings
- Logout/lock enforcement

## Migration from Old Schema

The system maintains backward compatibility:

```csharp
[Obsolete("Use AccountType instead")]
public bool IsSupervised
{
    get => AccountType == AccountType.Child;
    set => AccountType = value ? AccountType.Child : AccountType.Parent;
}
```

**Migration**:
- `IsSupervised = true` → `AccountType = Child`
- `IsSupervised = false` → `AccountType = Parent`

## Admin UI

### User List Display

```
Username    | Full Name      | Account Type | Status
------------|----------------|--------------|--------
johnny      | Johnny Smith   | Child        | Active
sarah       | Sarah Smith    | Child        | Active
mom         | Jane Smith     | Parent       | Active
dad         | John Smith     | Parent       | Active
backup      | Backup Service | Technical    | Active
```

### Filtering

- Show only children (default)
- Show all accounts
- Filter by account type

### Dashboard Statistics

- **Active Users**: Count of active Child accounts
- **Total Users**: All accounts
- **Parents**: Count of Parent accounts
- **Technical**: Count of Technical accounts

## Use Cases

### Family Setup

```
Children:
  - johnny (Child) - 2 hours/day limit
  - sarah (Child) - 1.5 hours/day limit

Parents:
  - mom (Parent) - No limits
  - dad (Parent) - No limits

Technical:
  - backup (Technical) - System backup account
  - plex (Technical) - Media server account
```

### School Setup

```
Students:
  - student1 (Child) - 1 hour/day limit
  - student2 (Child) - 1 hour/day limit
  ...

Teachers:
  - teacher1 (Parent) - No limits
  - teacher2 (Parent) - No limits

Technical:
  - admin (Technical) - System administration
  - printer (Technical) - Print service account
```

## Configuration Examples

### Child with Limits

```json
{
  "username": "johnny",
  "accountType": "Child",
  "timeProfile": {
    "mondayLimit": 120,
    "tuesdayLimit": 120,
    "weeklyLimit": 600,
    "enforcementAction": "logout"
  }
}
```

### Parent without Limits

```json
{
  "username": "mom",
  "accountType": "Parent"
}
```

No time profile needed - unlimited access.

### Technical Account

```json
{
  "username": "backup",
  "accountType": "Technical"
}
```

System account - never tracked or limited.

## Benefits

1. **Clear Classification**: Easy to identify account purpose
2. **Flexible Management**: Different rules for different account types
3. **No False Positives**: Parents/technical accounts not tracked
4. **Scalable**: Easy to add new account types in future
5. **Backward Compatible**: Old `IsSupervised` field still works

## Future Enhancements

Possible additional account types:
- **Guest**: Temporary accounts with strict limits
- **Teen**: Teenagers with different limit rules
- **Restricted**: Limited access to specific applications only

---

**Status**: ✅ Implemented  
**Database**: ✅ Schema updated  
**API**: ✅ Updated  
**Client**: ✅ Filters by account type  
**Backward Compatible**: ✅ Yes

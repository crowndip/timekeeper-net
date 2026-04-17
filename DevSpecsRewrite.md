# Parental Control System - Developer Specifications
## .NET 10 Rewrite with Centralized Architecture

**Version:** 1.0  
**Date:** April 17, 2026  
**Target Platform:** Linux (Ubuntu, Fedora, Debian)  
**Technology Stack:** .NET 10, PostgreSQL, Docker, systemd

---

## 1. Executive Summary

This document specifies a complete rewrite of the parental control functionality using modern .NET 10 architecture with centralized management. The system will consist of three main components:

1. **Central Web Service** - ASP.NET Core API with admin interface
2. **PostgreSQL Database** - Centralized data storage
3. **Client Agent** - Linux daemon for time tracking and enforcement

**Key Improvements over Original:**
- Centralized management across multiple computers
- Real-time synchronization
- Web-based administration
- Container-based deployment
- Modern, maintainable architecture
- RESTful API for extensibility

---

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Administrator                             │
│                    (Web Browser / Mobile)                        │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTPS
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Central Server (Docker)                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ASP.NET Core Web Service (.NET 10)                      │   │
│  │  - REST API (JSON)                                       │   │
│  │  - Admin Web UI (Blazor Server)                          │   │
│  │  - SignalR Hub (real-time updates)                       │   │
│  │  - Authentication & Authorization                        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                             │                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  PostgreSQL Database (Docker)                            │   │
│  │  - Users, Computers, Sessions                            │   │
│  │  - Time limits, Usage history                            │   │
│  │  - Configuration, Audit logs                             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                             │ HTTPS/REST
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Client Computers (Linux)                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Client Agent (.NET 10 Service)                          │   │
│  │  - Time tracking (1-minute intervals)                    │   │
│  │  - Session monitoring (systemd-logind)                   │   │
│  │  - Enforcement (logout, lock)                            │   │
│  │  - Server synchronization                                │   │
│  │  - Local cache (offline resilience)                      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                             │                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  User Notification UI (Required)                         │   │
│  │  - System tray icon                                      │   │
│  │  - Time remaining display                                │   │
│  │  - Notifications                                         │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Component Overview

| Component | Technology | Deployment | Purpose |
|-----------|-----------|------------|---------|
| Web Service | ASP.NET Core 10 | Docker Container | API + Admin UI |
| Database | PostgreSQL 16+ | Docker Container | Data persistence |
| Client Agent | .NET 10 Worker Service | systemd service | Time tracking |
| Admin UI | Blazor Server | Embedded in Web Service | Configuration |
| API | REST + SignalR | Embedded in Web Service | Client communication |

---

## 3. Database Schema

### 3.1 Core Tables

#### Users
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(64) NOT NULL UNIQUE,
    full_name VARCHAR(255),
    email VARCHAR(255),
    is_supervised BOOLEAN NOT NULL DEFAULT true,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT username_format CHECK (username ~ '^[a-z][a-z0-9_-]{0,31}$')
);

CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_supervised ON users(is_supervised) WHERE is_supervised = true;
```

#### Computers
```sql
CREATE TABLE computers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    hostname VARCHAR(255) NOT NULL UNIQUE,
    machine_id VARCHAR(64) NOT NULL UNIQUE,
    os_info VARCHAR(255),
    last_seen_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_computers_hostname ON computers(hostname);
CREATE INDEX idx_computers_active ON computers(is_active) WHERE is_active = true;
```

#### TimeProfiles
```sql
CREATE TABLE time_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    
    -- Daily limits (minutes per day, 0 = unlimited)
    monday_limit INT NOT NULL DEFAULT 0 CHECK (monday_limit >= 0),
    tuesday_limit INT NOT NULL DEFAULT 0 CHECK (tuesday_limit >= 0),
    wednesday_limit INT NOT NULL DEFAULT 0 CHECK (wednesday_limit >= 0),
    thursday_limit INT NOT NULL DEFAULT 0 CHECK (thursday_limit >= 0),
    friday_limit INT NOT NULL DEFAULT 0 CHECK (friday_limit >= 0),
    saturday_limit INT NOT NULL DEFAULT 0 CHECK (saturday_limit >= 0),
    sunday_limit INT NOT NULL DEFAULT 0 CHECK (sunday_limit >= 0),
    
    -- Weekly limit (minutes per week, 0 = unlimited)
    weekly_limit INT NOT NULL DEFAULT 0 CHECK (weekly_limit >= 0),
    
    -- Enforcement action
    enforcement_action VARCHAR(20) NOT NULL DEFAULT 'logout' 
        CHECK (enforcement_action IN ('logout', 'lock', 'notify')),
    
    -- Warning times (minutes before enforcement)
    warning_times INT[] NOT NULL DEFAULT ARRAY[15, 10, 5, 1],
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT unique_user_profile_name UNIQUE (user_id, name)
);

CREATE INDEX idx_time_profiles_user ON time_profiles(user_id);
CREATE INDEX idx_time_profiles_active ON time_profiles(is_active) WHERE is_active = true;
```

#### AllowedHours
```sql
CREATE TABLE allowed_hours (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id UUID NOT NULL REFERENCES time_profiles(id) ON DELETE CASCADE,
    day_of_week INT NOT NULL CHECK (day_of_week BETWEEN 0 AND 6), -- 0=Sunday
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    
    CONSTRAINT valid_time_range CHECK (start_time < end_time),
    CONSTRAINT unique_profile_day_time UNIQUE (profile_id, day_of_week, start_time, end_time)
);

CREATE INDEX idx_allowed_hours_profile ON allowed_hours(profile_id);
CREATE INDEX idx_allowed_hours_day ON allowed_hours(day_of_week);
```

#### Sessions
```sql
CREATE TABLE sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    computer_id UUID NOT NULL REFERENCES computers(id) ON DELETE CASCADE,
    
    session_start TIMESTAMPTZ NOT NULL,
    session_end TIMESTAMPTZ,
    
    -- Time tracking
    active_minutes INT NOT NULL DEFAULT 0,
    idle_minutes INT NOT NULL DEFAULT 0,
    
    -- Session state
    is_active BOOLEAN NOT NULL DEFAULT true,
    termination_reason VARCHAR(50),
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_sessions_user ON sessions(user_id);
CREATE INDEX idx_sessions_computer ON sessions(computer_id);
CREATE INDEX idx_sessions_active ON sessions(is_active) WHERE is_active = true;
CREATE INDEX idx_sessions_start ON sessions(session_start DESC);
```

#### TimeUsage
```sql
CREATE TABLE time_usage (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    computer_id UUID NOT NULL REFERENCES computers(id) ON DELETE CASCADE,
    session_id UUID REFERENCES sessions(id) ON DELETE SET NULL,
    
    usage_date DATE NOT NULL,
    minutes_used INT NOT NULL DEFAULT 0,
    
    -- Aggregation metadata
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT unique_user_computer_date UNIQUE (user_id, computer_id, usage_date)
);

CREATE INDEX idx_time_usage_user_date ON time_usage(user_id, usage_date DESC);
CREATE INDEX idx_time_usage_date ON time_usage(usage_date DESC);
```

#### TimeAdjustments
```sql
CREATE TABLE time_adjustments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    adjustment_date DATE NOT NULL,
    minutes_adjustment INT NOT NULL, -- Positive = add time, Negative = remove
    reason VARCHAR(500),
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT unique_user_adjustment_date UNIQUE (user_id, adjustment_date, created_at)
);

CREATE INDEX idx_time_adjustments_user ON time_adjustments(user_id);
CREATE INDEX idx_time_adjustments_date ON time_adjustments(adjustment_date DESC);
```

#### AuditLog
```sql
CREATE TABLE audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type VARCHAR(50) NOT NULL,
    entity_id UUID,
    action VARCHAR(50) NOT NULL,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    performed_by VARCHAR(100) NOT NULL,
    changes JSONB,
    ip_address INET,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_log_entity ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_log_user ON audit_log(user_id);
CREATE INDEX idx_audit_log_created ON audit_log(created_at DESC);
```

### 3.2 Views for Reporting

```sql
-- Current day usage summary
CREATE VIEW v_daily_usage AS
SELECT 
    u.id AS user_id,
    u.username,
    u.full_name,
    CURRENT_DATE AS usage_date,
    COALESCE(SUM(tu.minutes_used), 0) AS minutes_used_today,
    COALESCE(SUM(ta.minutes_adjustment), 0) AS adjustment_minutes,
    tp.monday_limit AS daily_limit, -- Simplified, actual would check day
    tp.weekly_limit
FROM users u
LEFT JOIN time_usage tu ON u.id = tu.user_id AND tu.usage_date = CURRENT_DATE
LEFT JOIN time_adjustments ta ON u.id = ta.user_id AND ta.adjustment_date = CURRENT_DATE
LEFT JOIN time_profiles tp ON u.id = tp.user_id AND tp.is_active = true
WHERE u.is_supervised = true AND u.is_active = true
GROUP BY u.id, u.username, u.full_name, tp.monday_limit, tp.weekly_limit;

-- Weekly usage summary
CREATE VIEW v_weekly_usage AS
SELECT 
    u.id AS user_id,
    u.username,
    DATE_TRUNC('week', CURRENT_DATE) AS week_start,
    COALESCE(SUM(tu.minutes_used), 0) AS minutes_used_week,
    tp.weekly_limit
FROM users u
LEFT JOIN time_usage tu ON u.id = tu.user_id 
    AND tu.usage_date >= DATE_TRUNC('week', CURRENT_DATE)
    AND tu.usage_date < DATE_TRUNC('week', CURRENT_DATE) + INTERVAL '7 days'
LEFT JOIN time_profiles tp ON u.id = tp.user_id AND tp.is_active = true
WHERE u.is_supervised = true AND u.is_active = true
GROUP BY u.id, u.username, tp.weekly_limit;
```

---

## 4. Web Service API Specification

### 4.1 Technology Stack

**Framework:** ASP.NET Core 10  
**API Style:** REST with JSON  
**Real-time:** SignalR for push notifications  
**Authentication:** JWT Bearer tokens  
**Documentation:** OpenAPI/Swagger  
**ORM:** Entity Framework Core 10

### 4.2 API Endpoints

#### Authentication

```
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
```

#### Client Agent Endpoints

```
POST   /api/client/register
  Body: { hostname, machineId, osInfo }
  Response: { computerId, apiKey }

POST   /api/client/heartbeat
  Body: { computerId, timestamp }
  Response: { serverTime, configVersion }

GET    /api/client/config/{computerId}
  Response: { users: [...], profiles: [...], allowedHours: [...] }

POST   /api/client/usage
  Body: { 
    computerId, 
    userId, 
    sessionId,
    timestamp,
    minutesActive,
    minutesIdle,
    isSessionActive
  }
  Response: { 
    timeRemaining, 
    shouldEnforce, 
    enforcementAction,
    warnings: [...]
  }

POST   /api/client/session/start
  Body: { computerId, userId, sessionStart }
  Response: { sessionId, timeRemaining }

POST   /api/client/session/end
  Body: { sessionId, sessionEnd, terminationReason }
  Response: { success }
```

#### Admin Endpoints

```
# Users
GET    /api/admin/users
POST   /api/admin/users
GET    /api/admin/users/{id}
PUT    /api/admin/users/{id}
DELETE /api/admin/users/{id}

# Time Profiles
GET    /api/admin/profiles
POST   /api/admin/profiles
GET    /api/admin/profiles/{id}
PUT    /api/admin/profiles/{id}
DELETE /api/admin/profiles/{id}

# Allowed Hours
GET    /api/admin/profiles/{profileId}/hours
POST   /api/admin/profiles/{profileId}/hours
DELETE /api/admin/profiles/{profileId}/hours/{id}

# Time Adjustments
POST   /api/admin/adjustments
  Body: { userId, date, minutesAdjustment, reason }

# Usage Reports
GET    /api/admin/usage/daily?userId={id}&date={date}
GET    /api/admin/usage/weekly?userId={id}&weekStart={date}
GET    /api/admin/usage/history?userId={id}&from={date}&to={date}

# Computers
GET    /api/admin/computers
GET    /api/admin/computers/{id}
PUT    /api/admin/computers/{id}

# Audit Log
GET    /api/admin/audit?entityType={type}&from={date}&to={date}
```

### 4.3 Data Transfer Objects (DTOs)

```csharp
// Request DTOs
public record RegisterComputerRequest(
    string Hostname,
    string MachineId,
    string OsInfo
);

public record UsageReportRequest(
    Guid ComputerId,
    Guid UserId,
    Guid? SessionId,
    DateTime Timestamp,
    int MinutesActive,
    int MinutesIdle,
    bool IsSessionActive
);

public record CreateUserRequest(
    string Username,
    string FullName,
    string? Email,
    bool IsSupervised
);

public record CreateTimeProfileRequest(
    Guid UserId,
    string Name,
    int MondayLimit,
    int TuesdayLimit,
    int WednesdayLimit,
    int ThursdayLimit,
    int FridayLimit,
    int SaturdayLimit,
    int SundayLimit,
    int WeeklyLimit,
    string EnforcementAction,
    int[] WarningTimes
);

public record CreateAllowedHoursRequest(
    Guid ProfileId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);

public record TimeAdjustmentRequest(
    Guid UserId,
    DateOnly Date,
    int MinutesAdjustment,
    string Reason
);

// Response DTOs
public record UsageReportResponse(
    int TimeRemainingMinutes,
    bool ShouldEnforce,
    string? EnforcementAction,
    int[] WarningMinutes
);

public record ClientConfigResponse(
    List<UserConfigDto> Users,
    List<TimeProfileDto> Profiles,
    List<AllowedHoursDto> AllowedHours
);

public record DailyUsageResponse(
    Guid UserId,
    string Username,
    DateOnly Date,
    int MinutesUsed,
    int MinutesAdjustment,
    int DailyLimit,
    int TimeRemaining
);
```


---

## 5. Client Agent Specification

### 5.1 Technology Stack

**Framework:** .NET 10 Worker Service  
**Deployment:** systemd service  
**Configuration:** appsettings.json + environment variables  
**Logging:** Serilog with file and console sinks  
**HTTP Client:** HttpClient with Polly for resilience

### 5.2 Core Responsibilities

1. **Session Monitoring**
   - Detect user logins via systemd-logind D-Bus interface
   - Track active/idle state
   - Detect session locks and user switches

2. **Time Tracking**
   - Count active minutes per user
   - Submit usage every 1 minute to server
   - Handle offline scenarios with local cache

3. **Enforcement**
   - Logout users when time expires
   - Lock screen if configured
   - Show warnings at configured intervals

4. **Synchronization**
   - Poll server for configuration updates
   - Sync time remaining across computers
   - Handle clock skew and time zone differences

### 5.3 Client Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Agent Service                      │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Main Worker (BackgroundService)                       │ │
│  │  - Orchestrates all operations                         │ │
│  │  - 1-minute timer loop                                 │ │
│  └────────────────────────────────────────────────────────┘ │
│                          │                                   │
│  ┌───────────────────────┴────────────────────────────────┐ │
│  │                                                         │ │
│  ▼                       ▼                       ▼         │ │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │ Session      │  │ Time         │  │ Server           │ │
│  │ Monitor      │  │ Tracker      │  │ Sync             │ │
│  │              │  │              │  │                  │ │
│  │ - D-Bus      │  │ - Count mins │  │ - API calls      │ │
│  │ - logind     │  │ - Local DB   │  │ - Config sync    │ │
│  │ - Events     │  │ - Aggregate  │  │ - Retry logic    │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
│         │                  │                    │           │
│  ┌──────┴──────────────────┴────────────────────┴────────┐ │
│  │                                                         │ │
│  ▼                                                         │ │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  Enforcement Engine                                   │ │
│  │  - Check limits                                       │ │
│  │  - Issue warnings                                     │ │
│  │  - Execute logout/lock                                │ │
│  └──────────────────────────────────────────────────────┘ │
│                          │                                   │
│  ┌───────────────────────┴────────────────────────────────┐ │
│  │                                                         │ │
│  ▼                       ▼                       ▼         │ │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │ Local Cache  │  │ Notification │  │ systemd-logind   │ │
│  │ (SQLite)     │  │ Service      │  │ Interface        │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 5.4 Key Classes

```csharp
// Main worker service
public class ParentalControlWorker : BackgroundService
{
    private readonly ISessionMonitor _sessionMonitor;
    private readonly ITimeTracker _timeTracker;
    private readonly IServerSyncService _serverSync;
    private readonly IEnforcementEngine _enforcement;
    private readonly ILogger<ParentalControlWorker> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessTickAsync();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    
    private async Task ProcessTickAsync()
    {
        // 1. Get active sessions
        var sessions = await _sessionMonitor.GetActiveSessionsAsync();
        
        // 2. Track time for each user
        foreach (var session in sessions)
        {
            await _timeTracker.RecordMinuteAsync(session);
        }
        
        // 3. Sync with server
        var usageData = await _timeTracker.GetPendingUsageAsync();
        var response = await _serverSync.SubmitUsageAsync(usageData);
        
        // 4. Check enforcement
        await _enforcement.CheckAndEnforceAsync(response);
    }
}

// Session monitoring
public interface ISessionMonitor
{
    Task<List<UserSession>> GetActiveSessionsAsync();
    Task<bool> IsSessionIdleAsync(string sessionId);
    Task<bool> IsSessionLockedAsync(string sessionId);
}

public class SystemdSessionMonitor : ISessionMonitor
{
    // Uses Tmds.DBus for systemd-logind integration
    private readonly IConnection _dbusConnection;
    
    public async Task<List<UserSession>> GetActiveSessionsAsync()
    {
        // Call org.freedesktop.login1.Manager.ListUsers
        // Filter for active graphical sessions
        // Return user session information
    }
}

// Time tracking
public interface ITimeTracker
{
    Task RecordMinuteAsync(UserSession session);
    Task<List<UsageRecord>> GetPendingUsageAsync();
    Task MarkAsSyncedAsync(List<Guid> recordIds);
}

public class TimeTracker : ITimeTracker
{
    private readonly ILocalCache _cache;
    
    public async Task RecordMinuteAsync(UserSession session)
    {
        var isIdle = await _sessionMonitor.IsSessionIdleAsync(session.Id);
        
        await _cache.IncrementUsageAsync(
            session.UserId,
            session.SessionId,
            activeMinutes: isIdle ? 0 : 1,
            idleMinutes: isIdle ? 1 : 0
        );
    }
}

// Server synchronization
public interface IServerSyncService
{
    Task<UsageReportResponse> SubmitUsageAsync(List<UsageRecord> records);
    Task<ClientConfigResponse> GetConfigurationAsync();
    Task<bool> RegisterComputerAsync();
}

public class ServerSyncService : IServerSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalCache _cache;
    
    public async Task<UsageReportResponse> SubmitUsageAsync(List<UsageRecord> records)
    {
        // Aggregate records by user
        // POST to /api/client/usage
        // Handle retries with Polly
        // Cache response for offline scenarios
    }
}

// Enforcement
public interface IEnforcementEngine
{
    Task CheckAndEnforceAsync(UsageReportResponse response);
    Task LogoutUserAsync(string username);
    Task LockSessionAsync(string sessionId);
    Task ShowWarningAsync(string username, int minutesRemaining);
}

public class EnforcementEngine : IEnforcementEngine
{
    private readonly ISessionMonitor _sessionMonitor;
    private readonly INotificationService _notifications;
    private readonly ISystemdLogind _logind;
    
    public async Task CheckAndEnforceAsync(UsageReportResponse response)
    {
        if (response.ShouldEnforce)
        {
            switch (response.EnforcementAction)
            {
                case "logout":
                    await LogoutUserAsync(response.Username);
                    break;
                case "lock":
                    await LockSessionAsync(response.SessionId);
                    break;
            }
        }
        
        // Check warnings
        foreach (var warningMinutes in response.WarningMinutes)
        {
            if (response.TimeRemainingMinutes == warningMinutes)
            {
                await ShowWarningAsync(response.Username, warningMinutes);
            }
        }
    }
    
    public async Task LogoutUserAsync(string username)
    {
        // Get session ID for user
        // Call org.freedesktop.login1.Manager.TerminateSession
        _logger.LogInformation("Logged out user {Username}", username);
    }
}

// Local cache (SQLite)
public interface ILocalCache
{
    Task IncrementUsageAsync(Guid userId, Guid sessionId, int activeMinutes, int idleMinutes);
    Task<List<UsageRecord>> GetPendingRecordsAsync();
    Task MarkAsSyncedAsync(List<Guid> recordIds);
    Task<ClientConfigResponse?> GetCachedConfigAsync();
    Task SaveConfigAsync(ClientConfigResponse config);
}
```

### 5.5 Configuration

**appsettings.json:**
```json
{
  "ParentalControl": {
    "ServerUrl": "https://parental-control.example.com",
    "ApiKey": "",
    "ComputerId": "",
    "TickIntervalSeconds": 60,
    "SyncRetryAttempts": 3,
    "SyncRetryDelaySeconds": 30,
    "OfflineMode": {
      "Enabled": true,
      "MaxOfflineHours": 24
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/parental-control/client.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

### 5.6 systemd Service Unit

**/etc/systemd/system/parental-control-client.service:**
```ini
[Unit]
Description=Parental Control Client Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/opt/parental-control/ParentalControl.Client
Restart=always
RestartSec=10
User=root
Environment=DOTNET_ENVIRONMENT=Production

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/log/parental-control /var/lib/parental-control

[Install]
WantedBy=multi-user.target
```

### 5.7 D-Bus Integration

**Dependencies:**
- Tmds.DBus NuGet package for D-Bus communication
- systemd-logind interface definitions

**Key D-Bus Interfaces:**
```csharp
// org.freedesktop.login1.Manager
public interface ILogin1Manager : IDBusObject
{
    Task<(uint, ObjectPath)[]> ListUsersAsync();
    Task<ObjectPath> GetUserAsync(uint uid);
    Task TerminateSessionAsync(string sessionId);
    Task LockSessionAsync(string sessionId);
}

// org.freedesktop.login1.User
public interface ILogin1User : IDBusObject
{
    Task<(string, ObjectPath)[]> GetSessionsAsync();
    Task<T> GetPropertyAsync<T>(string property);
}

// org.freedesktop.login1.Session
public interface ILogin1Session : IDBusObject
{
    Task<T> GetPropertyAsync<T>(string property);
    Task LockAsync();
    Task TerminateAsync();
}
```

---

## 6. Web Admin UI Specification

### 6.1 Technology Stack

**Framework:** Blazor Server (.NET 10)  
**UI Library:** MudBlazor or Radzen Blazor  
**Charts:** Blazor.Charts or ApexCharts  
**Authentication:** ASP.NET Core Identity  
**Authorization:** Role-based (Admin, Supervisor)

### 6.2 Page Structure

```
/
├── /login                    # Login page
├── /dashboard                # Overview dashboard
├── /users                    # User management
│   ├── /users/create
│   ├── /users/{id}/edit
│   └── /users/{id}/details
├── /profiles                 # Time profile management
│   ├── /profiles/create
│   ├── /profiles/{id}/edit
│   └── /profiles/{id}/hours  # Allowed hours configuration
├── /computers                # Computer management
│   └── /computers/{id}/details
├── /usage                    # Usage reports
│   ├── /usage/daily
│   ├── /usage/weekly
│   └── /usage/history
├── /adjustments              # Time adjustments
│   └── /adjustments/create
└── /audit                    # Audit log
```

### 6.3 Key Features

#### Dashboard
- Active users count
- Total computers online
- Today's usage summary (chart)
- Recent enforcement actions
- Quick actions (add time, view reports)

#### User Management
- List all supervised users
- Create/edit/delete users
- Assign time profiles
- View current status (online/offline, time remaining)
- Quick time adjustment

#### Time Profile Management
- Create reusable profiles
- Set daily limits per weekday
- Set weekly limits
- Configure allowed hours (time ranges per day)
- Set enforcement action (logout/lock/notify)
- Configure warning intervals

#### Usage Reports
- Daily usage per user (bar chart)
- Weekly trends (line chart)
- Historical data (date range selector)
- Export to CSV/PDF
- Filter by user, computer, date range

#### Time Adjustments
- Add/remove time for specific user and date
- Reason field (required)
- Audit trail

#### Computer Management
- View all registered computers
- Last seen timestamp
- Deactivate/reactivate computers
- View active sessions per computer

### 6.4 Real-time Updates

**SignalR Hub:**
```csharp
public class ParentalControlHub : Hub
{
    public async Task SubscribeToUser(Guid userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
    
    public async Task UnsubscribeFromUser(Guid userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}

// Server-side notifications
public class UsageNotificationService
{
    private readonly IHubContext<ParentalControlHub> _hubContext;
    
    public async Task NotifyUsageUpdateAsync(Guid userId, int minutesRemaining)
    {
        await _hubContext.Clients.Group($"user-{userId}")
            .SendAsync("UsageUpdated", new { userId, minutesRemaining });
    }
    
    public async Task NotifyEnforcementAsync(Guid userId, string action)
    {
        await _hubContext.Clients.Group($"user-{userId}")
            .SendAsync("EnforcementTriggered", new { userId, action });
    }
}
```

### 6.5 Sample Blazor Components

```razor
@* Dashboard.razor *@
@page "/dashboard"
@inject IUsageService UsageService
@inject NavigationManager Navigation

<PageTitle>Dashboard - Parental Control</PageTitle>

<MudGrid>
    <MudItem xs="12" sm="6" md="3">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.h6">Active Users</MudText>
                <MudText Typo="Typo.h3">@activeUsersCount</MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
    
    <MudItem xs="12" sm="6" md="3">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.h6">Computers Online</MudText>
                <MudText Typo="Typo.h3">@computersOnlineCount</MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
    
    <MudItem xs="12" md="6">
        <MudCard>
            <MudCardHeader>
                <CardHeaderContent>
                    <MudText Typo="Typo.h6">Today's Usage</MudText>
                </CardHeaderContent>
            </MudCardHeader>
            <MudCardContent>
                <MudChart ChartType="ChartType.Bar" 
                          ChartData="@usageChartData" 
                          XAxisLabels="@userLabels" />
            </MudCardContent>
        </MudCard>
    </MudItem>
</MudGrid>

@code {
    private int activeUsersCount;
    private int computersOnlineCount;
    private ChartData usageChartData;
    private string[] userLabels;
    
    protected override async Task OnInitializedAsync()
    {
        var stats = await UsageService.GetDashboardStatsAsync();
        activeUsersCount = stats.ActiveUsers;
        computersOnlineCount = stats.ComputersOnline;
        
        var usage = await UsageService.GetTodayUsageAsync();
        userLabels = usage.Select(u => u.Username).ToArray();
        usageChartData = new ChartData
        {
            Series = new List<ChartSeries>
            {
                new ChartSeries
                {
                    Name = "Minutes Used",
                    Data = usage.Select(u => (double)u.MinutesUsed).ToArray()
                }
            }
        };
    }
}
```

---

## 7. Deployment Specification

### 7.1 Docker Compose

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: parental-control-db
    environment:
      POSTGRES_DB: parental_control
      POSTGRES_USER: pcadmin
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql
    ports:
      - "5432:5432"
    networks:
      - parental-control-net
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pcadmin"]
      interval: 10s
      timeout: 5s
      retries: 5

  webservice:
    build:
      context: ./src/ParentalControl.WebService
      dockerfile: Dockerfile
    container_name: parental-control-web
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:80
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=parental_control;Username=pcadmin;Password=${DB_PASSWORD}"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: "ParentalControl"
      Jwt__Audience: "ParentalControlClients"
      Jwt__ExpirationMinutes: 60
    ports:
      - "8080:80"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - parental-control-net
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  postgres_data:

networks:
  parental-control-net:
    driver: bridge
```

### 7.2 Web Service Dockerfile

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ParentalControl.WebService.csproj", "./"]
RUN dotnet restore "ParentalControl.WebService.csproj"
COPY . .
RUN dotnet build "ParentalControl.WebService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ParentalControl.WebService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ParentalControl.WebService.dll"]
```

### 7.3 Client Installation Script

**install-client.sh:**
```bash
#!/bin/bash
set -e

# Variables
INSTALL_DIR="/opt/parental-control"
SERVICE_NAME="parental-control-client"
LOG_DIR="/var/log/parental-control"
DATA_DIR="/var/lib/parental-control"

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root"
    exit 1
fi

# Create directories
mkdir -p "$INSTALL_DIR"
mkdir -p "$LOG_DIR"
mkdir -p "$DATA_DIR"

# Download and extract client
echo "Downloading client..."
wget -O /tmp/client.tar.gz "https://releases.example.com/parental-control-client-latest.tar.gz"
tar -xzf /tmp/client.tar.gz -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/ParentalControl.Client"

# Create configuration
cat > "$INSTALL_DIR/appsettings.json" <<EOF
{
  "ParentalControl": {
    "ServerUrl": "${SERVER_URL}",
    "ApiKey": "",
    "ComputerId": "",
    "TickIntervalSeconds": 60
  }
}
EOF

# Install systemd service
cp "$INSTALL_DIR/parental-control-client.service" "/etc/systemd/system/"
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

# Register with server
echo "Registering with server..."
"$INSTALL_DIR/ParentalControl.Client" --register

# Start service
systemctl start "$SERVICE_NAME"

echo "Installation complete!"
echo "Service status:"
systemctl status "$SERVICE_NAME"
```


---

## 8. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)

**Database:**
- [ ] Set up PostgreSQL schema
- [ ] Create migrations with Entity Framework Core
- [ ] Implement seed data for testing
- [ ] Create database views

**Web Service Core:**
- [ ] Create ASP.NET Core project structure
- [ ] Configure Entity Framework Core
- [ ] Implement repository pattern
- [ ] Set up dependency injection
- [ ] Configure logging (Serilog)
- [ ] Implement health checks

**Authentication:**
- [ ] Implement JWT authentication
- [ ] Create admin user management
- [ ] Set up role-based authorization

### Phase 2: API Development (Weeks 3-4)

**Client API:**
- [ ] Computer registration endpoint
- [ ] Heartbeat endpoint
- [ ] Configuration sync endpoint
- [ ] Usage reporting endpoint
- [ ] Session management endpoints

**Admin API:**
- [ ] User CRUD endpoints
- [ ] Time profile CRUD endpoints
- [ ] Allowed hours endpoints
- [ ] Time adjustment endpoints
- [ ] Usage report endpoints
- [ ] Audit log endpoints

**Business Logic:**
- [ ] Time calculation engine
- [ ] Enforcement decision logic
- [ ] Weekly/daily limit aggregation
- [ ] Allowed hours validation
- [ ] Time adjustment processing

### Phase 3: Client Agent (Weeks 5-6)

**Core Services:**
- [ ] Worker service setup
- [ ] systemd-logind D-Bus integration
- [ ] Session monitoring
- [ ] Time tracking logic
- [ ] Local SQLite cache

**Server Communication:**
- [ ] HTTP client with retry logic
- [ ] Configuration synchronization
- [ ] Usage submission
- [ ] Offline mode handling

**Enforcement:**
- [ ] Logout implementation
- [ ] Lock screen implementation
- [ ] Warning notifications
- [ ] Graceful shutdown handling

### Phase 4: Web Admin UI (Weeks 7-8)

**Core Pages:**
- [ ] Login page
- [ ] Dashboard with statistics
- [ ] User management pages
- [ ] Time profile management
- [ ] Allowed hours configuration

**Reports:**
- [ ] Daily usage reports
- [ ] Weekly usage reports
- [ ] Historical data views
- [ ] Export functionality

**Real-time Features:**
- [ ] SignalR hub setup
- [ ] Live usage updates
- [ ] Enforcement notifications

### Phase 5: Testing & Deployment (Weeks 9-10)

**Testing:**
- [ ] Unit tests for business logic
- [ ] Integration tests for API
- [ ] Client agent testing on multiple distros
- [ ] Load testing
- [ ] Security testing

**Deployment:**
- [ ] Docker images
- [ ] Docker Compose configuration
- [ ] Client installation scripts
- [ ] Documentation
- [ ] Monitoring setup

**Documentation:**
- [ ] API documentation (Swagger)
- [ ] Admin user guide
- [ ] Installation guide
- [ ] Troubleshooting guide

---

## 9. Technical Requirements

### 9.1 Development Environment

**Required Software:**
- .NET 10 SDK
- PostgreSQL 16+
- Docker & Docker Compose
- Visual Studio 2022 or JetBrains Rider
- Git

**Recommended Tools:**
- Postman or Insomnia (API testing)
- pgAdmin or DBeaver (database management)
- Linux VM or WSL2 (client testing)

### 9.2 NuGet Packages

**Web Service:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
<PackageReference Include="MudBlazor" Version="7.*" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />
```

**Client Agent:**
```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.*" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.*" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Tmds.DBus" Version="0.15.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.*" />
<PackageReference Include="System.CommandLine" Version="2.*" />
```

### 9.3 Performance Requirements

**Web Service:**
- Response time: < 200ms for 95th percentile
- Throughput: 1000 requests/second
- Concurrent users: 100+
- Database connections: Pool of 20-50

**Client Agent:**
- CPU usage: < 1% average
- Memory usage: < 50MB
- Disk I/O: Minimal (batch writes)
- Network: < 10KB/minute per client

**Database:**
- Query response: < 50ms for common queries
- Concurrent connections: 50+
- Storage: ~1GB per 1000 users/year

### 9.4 Security Requirements

**Authentication:**
- JWT tokens with 1-hour expiration
- Refresh token mechanism
- API key authentication for clients
- Password hashing with bcrypt

**Authorization:**
- Role-based access control (Admin, Supervisor)
- Client API key per computer
- Audit logging for all admin actions

**Data Protection:**
- HTTPS/TLS 1.3 for all communication
- Encrypted database connections
- Secure password storage
- Input validation and sanitization

**Client Security:**
- Run as root (required for enforcement)
- Secure API key storage
- Certificate pinning (optional)
- Tamper detection (future)

### 9.5 Reliability Requirements

**Availability:**
- Web service: 99.9% uptime
- Database: 99.9% uptime
- Client agent: Automatic restart on failure

**Data Integrity:**
- Transaction support for critical operations
- Atomic usage updates
- Backup and restore procedures
- Data validation at all layers

**Fault Tolerance:**
- Client offline mode (up to 24 hours)
- Automatic retry with exponential backoff
- Circuit breaker pattern for external calls
- Graceful degradation

**Monitoring:**
- Health check endpoints
- Application metrics (Prometheus)
- Log aggregation (ELK stack or similar)
- Alerting for critical errors

---

## 10. Data Flow Examples

### 10.1 User Login Flow

```
1. User logs into Linux computer
   ↓
2. systemd-logind emits UserNew signal
   ↓
3. Client agent detects new session via D-Bus
   ↓
4. Client calls POST /api/client/session/start
   {
     "computerId": "uuid",
     "userId": "uuid",
     "sessionStart": "2026-04-17T08:00:00Z"
   }
   ↓
5. Server creates session record
   ↓
6. Server calculates time remaining
   ↓
7. Server responds with:
   {
     "sessionId": "uuid",
     "timeRemaining": 120,
     "warnings": [15, 10, 5, 1]
   }
   ↓
8. Client stores session ID and starts tracking
```

### 10.2 Time Tracking Flow (Every Minute)

```
1. Client timer fires (every 60 seconds)
   ↓
2. Client checks session state via D-Bus
   - Is session active?
   - Is screen locked?
   - Is user idle?
   ↓
3. Client increments local counter
   - activeMinutes++ if active
   - idleMinutes++ if idle
   ↓
4. Client calls POST /api/client/usage
   {
     "computerId": "uuid",
     "userId": "uuid",
     "sessionId": "uuid",
     "timestamp": "2026-04-17T08:15:00Z",
     "minutesActive": 1,
     "minutesIdle": 0,
     "isSessionActive": true
   }
   ↓
5. Server updates time_usage table
   ↓
6. Server calculates remaining time:
   - Get daily limit for today
   - Get weekly limit
   - Get total used today
   - Get total used this week
   - Check allowed hours
   - Apply adjustments
   - Calculate: remaining = min(daily_remaining, weekly_remaining)
   ↓
7. Server checks enforcement:
   - If remaining <= 0: shouldEnforce = true
   - If remaining in warnings: include in response
   ↓
8. Server responds:
   {
     "timeRemainingMinutes": 105,
     "shouldEnforce": false,
     "enforcementAction": null,
     "warningMinutes": [15, 10, 5, 1]
   }
   ↓
9. Client processes response:
   - If shouldEnforce: execute logout/lock
   - If warning: show notification
   - Update local cache
```

### 10.3 Time Adjustment Flow

```
1. Admin opens web UI
   ↓
2. Admin navigates to user details
   ↓
3. Admin clicks "Add Time"
   ↓
4. Admin fills form:
   - User: John Doe
   - Date: Today
   - Minutes: +30
   - Reason: "Good behavior"
   ↓
5. UI calls POST /api/admin/adjustments
   {
     "userId": "uuid",
     "date": "2026-04-17",
     "minutesAdjustment": 30,
     "reason": "Good behavior"
   }
   ↓
6. Server validates request
   ↓
7. Server creates time_adjustments record
   ↓
8. Server logs to audit_log
   ↓
9. Server notifies via SignalR:
   - Send to admin UI: "Adjustment created"
   - Send to user group: "Time added"
   ↓
10. Next client sync will receive updated time remaining
```

### 10.4 Enforcement Flow

```
1. Client receives usage response:
   {
     "timeRemainingMinutes": 0,
     "shouldEnforce": true,
     "enforcementAction": "logout"
   }
   ↓
2. Client checks enforcement action
   ↓
3. For "logout":
   a. Get session ID for user
   b. Call D-Bus: org.freedesktop.login1.Manager.TerminateSession(sessionId)
   c. Log enforcement action
   d. Notify server of termination
   ↓
4. systemd-logind terminates user session
   ↓
5. User is logged out
   ↓
6. Client calls POST /api/client/session/end
   {
     "sessionId": "uuid",
     "sessionEnd": "2026-04-17T10:00:00Z",
     "terminationReason": "time_limit_exceeded"
   }
   ↓
7. Server updates session record
   ↓
8. Server notifies admin via SignalR
```

---

## 11. Error Handling & Edge Cases

### 11.1 Network Failures

**Client Cannot Reach Server:**
- Client enters offline mode
- Continue tracking time locally in SQLite
- Use last known configuration
- Queue usage reports for later submission
- When connection restored: bulk submit queued data
- Maximum offline period: 24 hours (configurable)

**Server Cannot Reach Database:**
- Return HTTP 503 Service Unavailable
- Client retries with exponential backoff
- Admin UI shows error banner
- Health check endpoint reports unhealthy

### 11.2 Clock Skew

**Client Clock Ahead of Server:**
- Server validates timestamp is not too far in future (max 5 minutes)
- Reject request if timestamp invalid
- Client logs warning and syncs with server time

**Client Clock Behind Server:**
- Server accepts historical data within reason (max 1 hour)
- Adjust usage records to correct date/time
- Log discrepancy for audit

### 11.3 Concurrent Sessions

**User Logged In on Multiple Computers:**
- Each client reports usage independently
- Server aggregates usage across all computers
- Time limit applies globally (not per computer)
- When limit reached: all computers enforce simultaneously

**User Switches Between Computers:**
- Old computer detects session end
- New computer starts new session
- Server maintains single time pool
- Seamless experience for user

### 11.4 Configuration Changes

**Admin Changes Time Limit While User Active:**
- Server recalculates time remaining
- Next client sync receives updated limit
- If new limit already exceeded: immediate enforcement
- If new limit grants more time: user continues

**Admin Changes Allowed Hours:**
- Server checks if current time is now disallowed
- If disallowed: mark for enforcement on next sync
- Client enforces on next tick

### 11.5 System Events

**Computer Suspend/Resume:**
- Client detects resume via systemd signal
- Sync with server immediately
- Adjust for time passed during suspend
- Continue normal operation

**Client Service Restart:**
- Load state from local cache
- Sync with server
- Resume monitoring active sessions
- No time lost (server is source of truth)

**Database Backup/Maintenance:**
- Use read replicas for queries during backup
- Schedule maintenance during low-usage periods
- Notify admins of planned downtime

---

## 12. Testing Strategy

### 12.1 Unit Tests

**Web Service:**
- Time calculation logic
- Enforcement decision logic
- Validation rules
- DTO mapping
- Business rules

**Client Agent:**
- Time tracking logic
- Offline mode handling
- Configuration parsing
- Local cache operations

**Coverage Target:** 80%+ for business logic

### 12.2 Integration Tests

**API Tests:**
- All endpoints with valid/invalid data
- Authentication and authorization
- Database transactions
- Concurrent requests

**Client-Server Integration:**
- Registration flow
- Usage reporting
- Configuration sync
- Enforcement flow

### 12.3 End-to-End Tests

**Scenarios:**
1. User logs in, uses computer, time expires, gets logged out
2. Admin adds time, user continues working
3. User switches computers, time syncs correctly
4. Client goes offline, comes back online, data syncs
5. Multiple users on same computer, each tracked separately

### 12.4 Performance Tests

**Load Testing:**
- 100 concurrent clients reporting usage
- 1000 requests/second to API
- Database query performance under load

**Stress Testing:**
- Maximum concurrent sessions
- Large historical data queries
- Bulk data import/export

### 12.5 Security Tests

**Penetration Testing:**
- SQL injection attempts
- XSS attempts in admin UI
- Authentication bypass attempts
- Authorization escalation attempts

**Vulnerability Scanning:**
- Dependency scanning (Snyk, Dependabot)
- Container image scanning
- OWASP Top 10 compliance

---

## 13. Monitoring & Observability

### 13.1 Metrics

**Application Metrics:**
- Request rate (requests/second)
- Response time (p50, p95, p99)
- Error rate (errors/second)
- Active sessions count
- Enforcement actions count

**System Metrics:**
- CPU usage
- Memory usage
- Disk I/O
- Network I/O

**Business Metrics:**
- Total users
- Active users today
- Total time tracked (minutes)
- Enforcement actions per day

### 13.2 Logging

**Log Levels:**
- ERROR: Failures requiring attention
- WARNING: Potential issues
- INFORMATION: Normal operations
- DEBUG: Detailed troubleshooting

**Structured Logging:**
```json
{
  "timestamp": "2026-04-17T10:15:30Z",
  "level": "Information",
  "message": "Usage reported",
  "userId": "uuid",
  "computerId": "uuid",
  "minutesActive": 1,
  "timeRemaining": 105
}
```

**Log Aggregation:**
- Centralized logging (ELK, Loki, or CloudWatch)
- Retention: 30 days for INFO, 90 days for ERROR
- Search and filter capabilities

### 13.3 Alerting

**Critical Alerts:**
- Service down (web service or database)
- High error rate (> 5%)
- Database connection failures
- Disk space low (< 10%)

**Warning Alerts:**
- High response time (> 500ms)
- High CPU usage (> 80%)
- High memory usage (> 80%)
- Client offline > 1 hour

**Notification Channels:**
- Email
- Slack/Teams
- PagerDuty (for critical)

### 13.4 Health Checks

**Web Service:**
```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database")
    .AddCheck<ClientConnectivityHealthCheck>("client-connectivity")
    .AddCheck("disk-space", () => CheckDiskSpace());
```

---

## 14. Future Enhancements

### 14.1 Phase 2 Features

**Application-Specific Limits (PlayTime):**
- Track specific applications/games
- Separate time limits per application
- Process monitoring on client
- Application usage reports

**Mobile App:**
- iOS/Android app for admins
- View usage statistics
- Add time on-the-go
- Push notifications

**Advanced Reporting:**
- Custom report builder
- Scheduled reports (email)
- Data export (CSV, PDF, Excel)
- Trend analysis

### 14.2 Phase 3 Features

**Multi-Tenancy:**
- Support multiple organizations
- Tenant isolation
- Per-tenant configuration
- Billing integration

**Advanced Scheduling:**
- Recurring time adjustments
- Holiday schedules
- School year vs. summer schedules
- Automatic profile switching

**Gamification:**
- Reward system for good behavior
- Achievement badges
- Time banking (save unused time)
- Parental approval workflows

### 14.3 Technical Improvements

**Performance:**
- Redis caching layer
- Read replicas for reporting
- CDN for static assets
- GraphQL API option

**Scalability:**
- Kubernetes deployment
- Horizontal scaling
- Message queue (RabbitMQ/Kafka)
- Microservices architecture

**Security:**
- Two-factor authentication
- Certificate-based client auth
- Encrypted local cache
- Tamper detection

---

## 15. Appendix

### 15.1 Glossary

- **Enforcement Action**: Action taken when time limit is reached (logout, lock, notify)
- **Time Profile**: Configuration of time limits and allowed hours for a user
- **Allowed Hours**: Time ranges when computer usage is permitted
- **Time Adjustment**: Manual addition or removal of time by admin
- **Session**: Period from user login to logout
- **Active Minutes**: Time when user is actively using computer
- **Idle Minutes**: Time when user is logged in but inactive

### 15.2 References

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [systemd-logind D-Bus API](https://www.freedesktop.org/wiki/Software/systemd/logind/)
- [Tmds.DBus Documentation](https://github.com/tmds/Tmds.DBus)
- [MudBlazor Components](https://mudblazor.com/)

### 15.3 Contact & Support

**Project Repository**: TBD  
**Issue Tracker**: TBD  
**Documentation**: TBD  
**Support Email**: TBD

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-04-17 | Development Team | Initial specification |

---

**End of Developer Specifications**

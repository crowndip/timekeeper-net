# Features Overview

**Version**: v1.4.0  
**Status**: ✅ Production Ready

## Core Features

### Time Management
- ✅ **Per-Day Limits**: Different limits for each day of the week (Monday-Sunday)
- ✅ **Weekly Limits**: Total time limit across the entire week
- ✅ **Allowed Hours**: Restrict usage to specific time windows (e.g., 9:00-17:00)
- ✅ **Real-Time Tracking**: Minute-by-minute usage monitoring
- ✅ **Cross-Device Aggregation**: Time shared across all user's devices

### User Management
- ✅ **Auto-User Creation**: Users automatically created on first login
- ✅ **Account Types**: Parent, Child, Unassigned
- ✅ **Username Normalization**: Case-insensitive ("John" = "john" = "JOHN")
- ✅ **Multiple Profiles**: Different time limits per user
- ✅ **Profile Switching**: Activate different profiles for different scenarios

### Security & Authentication (v1.4.0)
- ✅ **Dashboard Login**: Password-protected admin interface
- ✅ **Two-Tier Security**: Separate passwords for viewing and editing
- ✅ **Read-Only Mode**: View-only access by default
- ✅ **Session Management**: 8-hour session timeout
- ✅ **Input Validation**: Comprehensive validation on all inputs

### Emergency Features (v1.4.0)
- ✅ **Time Adjustment**: Grant extra time for exceptional situations
- ✅ **Quick Actions**: +5, +15, +30, +60 minute buttons
- ✅ **Date-Specific**: Adjustments valid only for current day
- ✅ **Audit Trail**: Reason tracking for all adjustments
- ✅ **Multiple Adjustments**: Sum correctly for same day

### Enforcement
- ✅ **Automatic Logout**: Force logout when time expires
- ✅ **Session Locking**: Lock screen instead of logout (configurable)
- ✅ **Warning Notifications**: Alerts at 15, 10, 5, 1 minutes remaining
- ✅ **Grace Period**: Configurable grace period before enforcement
- ✅ **Parent Bypass**: Parent accounts not subject to limits

### Cross-Platform Support
- ✅ **Linux Client**: systemd integration, D-Bus session monitoring
- ✅ **Windows Client**: Windows Service, WTS session monitoring
- ✅ **Unified Tracking**: Same user across platforms = shared time
- ✅ **Platform-Specific UI**: Avalonia (Linux), WPF (Windows)

### Offline Mode
- ✅ **Local Caching**: Continue tracking when server unavailable
- ✅ **Automatic Sync**: Sync usage when connection restored
- ✅ **Last Known Limits**: Use cached limits during offline periods
- ✅ **Resilient**: Graceful degradation without server

### Administration
- ✅ **Web Dashboard**: Blazor Server UI
- ✅ **User Management**: Create, edit, delete users
- ✅ **Profile Management**: Create, edit, delete time profiles
- ✅ **Computer Management**: View registered devices, regenerate API keys
- ✅ **Usage Reports**: View time usage per user/day
- ✅ **Real-Time Status**: See current sessions and time remaining

### API & Integration
- ✅ **RESTful API**: Well-documented endpoints
- ✅ **Swagger UI**: Interactive API documentation
- ✅ **Device Registration**: Automatic device registration with API keys
- ✅ **Health Checks**: Monitor service health
- ✅ **Structured Logging**: Serilog with file and console output

### Data Management
- ✅ **PostgreSQL Database**: Reliable, scalable storage
- ✅ **Entity Framework Core**: Type-safe data access
- ✅ **Migrations**: Database schema versioning
- ✅ **Cascade Deletes**: Automatic cleanup of related data
- ✅ **Audit Fields**: CreatedAt, UpdatedAt, CreatedBy tracking

### Deployment
- ✅ **Docker Support**: Containerized deployment
- ✅ **Docker Compose**: Single-command deployment
- ✅ **Portainer Compatible**: Easy deployment via Portainer
- ✅ **Environment Variables**: Configurable via environment
- ✅ **Health Checks**: Docker health monitoring

## Feature Matrix

| Feature | Linux | Windows | Status |
|---------|-------|---------|--------|
| Time Tracking | ✅ | ✅ | Production |
| Enforcement | ✅ | ✅ | Production |
| Offline Mode | ✅ | ✅ | Production |
| Notifications | ✅ | ✅ | Production |
| Session Monitoring | ✅ | ✅ | Production |
| Auto-Start | ✅ | ✅ | Production |
| System Integration | systemd | Windows Service | Production |

## Roadmap

### Planned Features
- 📋 **Email Notifications**: Alert parents when limits reached
- 📋 **Mobile App**: iOS/Android companion app
- 📋 **Reports**: Weekly/monthly usage reports
- 📋 **Application Blocking**: Block specific applications
- 📋 **Website Filtering**: Block specific websites
- 📋 **Screen Time Insights**: Usage patterns and recommendations

### Under Consideration
- 💭 **Reward System**: Earn extra time for good behavior
- 💭 **Homework Mode**: Temporary unlimited access for schoolwork
- 💭 **Bedtime Enforcement**: Automatic shutdown at bedtime
- 💭 **Multi-Tenant**: Support multiple families on one server

## Technical Specifications

### Server
- **Framework**: ASP.NET Core 8
- **Database**: PostgreSQL 16+
- **UI**: Blazor Server
- **API**: RESTful with Swagger
- **Logging**: Serilog

### Client
- **Framework**: .NET 8
- **Service**: Worker Service (systemd/Windows Service)
- **UI**: Avalonia (Linux), WPF (Windows)
- **Communication**: HTTP/REST
- **Storage**: SQLite (local cache)

### Requirements
- **Server**: Docker, 512MB RAM, 1GB disk
- **Client**: .NET 8 Runtime, 100MB RAM, 50MB disk
- **Network**: HTTP access to server (port 8080)

## Testing

- ✅ **66 Unit Tests**: 100% passing
- ✅ **Scenario Tests**: Real-world workflow coverage
- ✅ **Validation Tests**: Input validation coverage
- ✅ **Integration Tests**: Controller and service tests
- ✅ **CI/CD Ready**: Automated testing on push

## Documentation

- ✅ **README**: Quick start guide
- ✅ **API Docs**: Swagger UI
- ✅ **Installation**: Step-by-step guides
- ✅ **Configuration**: Environment variables
- ✅ **Troubleshooting**: Common issues
- ✅ **Architecture**: System design docs

## Support

- **GitHub Issues**: Bug reports and feature requests
- **Documentation**: Comprehensive guides
- **Examples**: Sample configurations
- **Community**: Active development

---

**Last Updated**: 2026-04-17  
**Version**: v1.4.0  
**Status**: ✅ Production Ready

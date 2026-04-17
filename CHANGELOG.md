# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-04-17

### Added
- **Dashboard Authentication**: Password-protected admin interface with `AdminPassword` environment variable
- **Two-Tier Security**: Separate `LimitAdministratorPassword` for administrative operations
- **Read-Only Mode**: Dashboard defaults to view-only, requires admin password to edit
- **Emergency Time Adjustment**: Quick time grants (+5, +15, +30, +60 minutes) for exceptional situations
- **Username Normalization**: Case-insensitive username handling (prevents "John" vs "john" duplicates)
- **Input Validation**: Comprehensive validation on all API endpoints and UI forms
  - Time limits: 0-1440 minutes
  - Usernames: alphanumeric + `_`, `-`, `.` (max 64 chars)
  - Emails: RFC-compliant (max 255 chars)
  - Profile names: 1-100 characters
- **Test Coverage Expansion**: Added 10 comprehensive user scenario tests (66 total tests, 100% passing)

### Changed
- **User Identification**: Server now determines userId from username (clients send username, not userId)
- **Cross-Platform Unification**: Same username across Linux/Windows = same user account
- **Session Management**: 8-hour session timeout with sessionStorage-based authentication

### Fixed
- **Windows Client**: Fixed critical userId bug (was using client-provided userId instead of server lookup)
- **Case Sensitivity**: Username matching now case-insensitive across all operations

### Security
- Dashboard login required (default password: "admin", configurable via environment)
- Administrator authentication for edit operations
- Input validation prevents injection attacks
- API key generation for device registration

## [1.3.0] - 2026-04-15

### Added
- Cross-platform user identification
- Automatic user creation on first login
- Username-based user lookup

### Changed
- Client no longer generates userId locally
- Server manages all user identity

## [1.2.0] - 2026-04-14

### Added
- Windows client support
- WPF notification UI for Windows
- Cross-platform time aggregation

### Fixed
- Time calculation bugs
- Session tracking issues

## [1.1.0] - 2026-04-12

### Added
- Offline mode support
- Local caching for resilience
- Automatic reconnection logic

### Changed
- Improved error handling
- Better logging

## [1.0.1] - 2026-04-10

### Fixed
- Client installation script permissions
- Service startup issues
- Database migration errors

## [1.0.0] - 2026-04-08

### Added
- Initial release
- Linux client with systemd integration
- ASP.NET Core 8 web service
- PostgreSQL database
- Blazor Server admin UI
- Time tracking and enforcement
- Per-day and weekly limits
- Allowed hours configuration
- Real-time usage monitoring
- Docker deployment support

[1.4.0]: https://github.com/crowndip/timekeeper-net/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/crowndip/timekeeper-net/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/crowndip/timekeeper-net/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/crowndip/timekeeper-net/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/crowndip/timekeeper-net/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/crowndip/timekeeper-net/releases/tag/v1.0.0

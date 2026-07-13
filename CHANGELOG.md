# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.68.1] - 2026-07-13

Follow-up fixes from an independent post-implementation review of 1.68.0. See `work/fable-review-2.md` (findings) and `work/sonnet-progress2.md` (implementation detail) for full context.

### Fixed
- **Crash on suppressed usage reports against PostgreSQL**: a brand-new usage row created for a suppressed (concurrent) report was stamped with `DateTime.MinValue`, which Npgsql rejects for a `timestamptz` column (`Kind=Unspecified`) — this 500'd in production while passing all tests, since the test suite runs on EF's InMemory provider, which doesn't enforce `DateTime.Kind`. Now uses `DateTime.UnixEpoch`.
- **A locked second computer could prevent the active computer's time from counting**: zero-minute usage reports (a locked session's idle tick, a new-session probe, the startup user sync) were able to "win" the concurrent-usage suppression window purely by reporting first, permanently suppressing a second, actively-used computer's real minutes — i.e. unlimited time as long as a second machine sat locked and logged in. Zero-minute reports no longer claim the window.
- **Usage recorded while the server was unreachable could be lost**: a backlog of usage flushed after an offline stretch was suppressed (and its minutes never recorded) if another computer happened to be active when it synced, even though the client marks a successful sync as complete regardless. Backlog flushes (reports with an old timestamp) now always count in full and never suppress another computer's current activity.
- **A stuck `loginctl` call could hang the client's tracking loop indefinitely**: all `loginctl` invocations in the session monitor now run under a 10-second timeout (killing the process tree on expiry) instead of waiting forever; a failed/timed-out query now safely degrades (empty session list, or "assume active") instead of blocking every subsequent tick.
- **The logout ladder's own session-verification check could still hang forever**: the `loginctl` call used to confirm a user actually logged off (run between every step of the logout ladder, and after the deadline fallback) had no timeout of its own, so a wedged `logind` there defeated the 90-second deadline entirely and left enforcement permanently disabled for that user until a service restart. It now runs under the same 10-second timeout as every other external call.
- **Windows machine identity was hostname-derived**: renaming a Windows PC (or two PCs sharing a name) used to change/collide its server-side identity and API key. Now uses the stable `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`, falling back to the old hostname-based scheme only if the registry read fails. Existing Windows installs will register as a new computer on first startup after this upgrade (harmless for time budgets, which are tracked per user, not per computer; the old computer row becomes stale in the admin UI).
- **No automatic recovery if the server's database was restored from backup**: both clients now force re-registration after 10 consecutive usage-submission failures, self-healing a foreign-key mismatch against a reset database without requiring a manual service restart.
- **Logout enforcement could keep escalating past its own deadline**: the 90-second logout deadline now actually cancels the in-progress logout ladder instead of racing it and leaving it to run detached, which could let it escalate further, release the anti-double-launch guard early, or swallow an exception.
- **Two simultaneous usage reports for the same child could both count**: the usage-recording read-check-write sequence is now wrapped in a serializable database transaction (PostgreSQL only; retried once on a serialization conflict), closing a narrow race where two reports landing within milliseconds of each other could both read "no recent activity" and both count.
- Login-throttle tracking is now pruned to avoid unbounded memory growth from spoofed source IPs; documented that `TrustForwardedHeaders` requires the web service to be unreachable except through the reverse proxy, or the throttle itself becomes spoofable.
- pgadmin's Docker Compose port binding is now `127.0.0.1`-only (was exposed to the whole LAN with default credentials).
- Windows client data directory (`C:\ProgramData\ParentalControl`) is now write-protected to SYSTEM/Administrators only, so a local user can no longer tamper with the server URL, cached limits, or registration files (still readable, since the tray app runs in the user's own session and needs to read some of the same files).
- **The Windows installer's permission-hardening could silently fail on non-English Windows**: it granted access by English group name (`SYSTEM`/`Administrators`/`Users`), which doesn't resolve on a localized OS (e.g. Czech) and could leave the data directory with an empty ACL that nobody, including the service, can access. Now uses locale-independent well-known SIDs, and warns instead of failing silently if permissioning still doesn't apply.
- Local cache reads are now consistently synchronized with the same lock used for writes; cache saves now flush to disk before the atomic rename, closing a narrow power-loss corruption window.
- `build-deb.sh` now ships the same hardened, `ReadWritePaths`-correct systemd unit used elsewhere instead of a second, drifted copy; fixed an upgrade-notification message that was silently printing empty due to heredoc quoting.
- Removed dead `_dailyUsage` cache state (superseded by the 1.68.0 offline-math fix) from both clients.

Robustness/stability pass covering server time-tracking correctness, client crash-proofing and offline persistence, client API authentication, enforcement reliability, and several smaller server fixes. See `work/fable-comments.md` (source review) and `work/sonet-progress.md` (implementation detail) for full context.

### Fixed
- **Concurrent-usage double-counting/deadlock**: two computers used by the same child at once now correctly count wall-clock time once (previously either double-counted, or stopped counting entirely once both machines were active) (#2).
- **Timezone handling**: daily reset, allowed-hours windows, and weekly boundaries are now computed in household local time via a new `IClockService`, instead of UTC (#3). New settings: `ParentalControl:TimeZoneId`, `FirstDayOfWeek`.
- **Username case mismatch**: usernames are now normalized consistently everywhere a user is created or looked up, so "Alice" (parent-created) and "alice" (client-reported) resolve to the same account (#8).
- **First-contact registration race**: two computers reporting a brand-new username/computer at the same instant no longer 500s one of them.
- **Weekly limit adjustments**: a bonus granted earlier in the week now correctly applies to the weekly total on later days (#13).
- **Offline enforcement math**: fixed a double-subtraction bug that could log a child out far earlier than their actual limit allowed while offline (#5); cache state is now keyed by username instead of a placeholder GUID, fixing shared-computer offline enforcement using the wrong user's limits (#4).
- **Client crash-proofing**: a missing/invalid server URL no longer crash-loops the client service; registration now retries automatically until it succeeds (#10).
- **Local cache persistence**: pending usage, cached limits, and daily usage now survive a reboot while the server is unreachable, instead of being lost from memory (#7).
- **Batched usage now grouped by date**: an offline stretch spanning midnight no longer books later minutes onto an earlier date (#12).
- **Client API authentication**: `/api/client/*` endpoints now validate an API key issued at registration, with a `RequireClientApiKey` grace-mode setting (default off) so already-deployed clients keep working until upgraded; the client stores its key and self-heals via re-registration on a 401.
- **Registration hardening**: computers are now matched by machine ID only (not hostname), so a hostname collision can no longer hijack another computer's record/key (#18).
- **Enforcement no longer blocks the client's tick loop**: logout/lock now run in the background with an overall 90-second deadline and a final forced fallback, instead of the worker's whole tracking loop waiting on a potentially-stuck logout attempt. Added a MATE desktop CLI logout fallback.
- **Login throttling**: the admin password endpoint now locks out an IP after repeated failures and uses constant-time comparison.
- Merged adjacent/overlapping allowed-hours windows so a warning no longer implies a cutoff that doesn't actually happen.
- `GetTimeStatus` now reports usage consistently for users with aliases.
- Various small fixes: proxy password file permissions, `/etc/passwd` parsing robustness, configurable DataProtection key path.
- Fixed a pre-existing EF Core migration/model-snapshot drift bug found while adding a new migration (unrelated to the above, would have broken any future migration on an already-upgraded database).

## [1.45.0] - 2026-04-21

### Added
- **Multi-Language Support**: Complete internationalization with 5 languages
  - 🇬🇧 English (en)
  - 🇨🇿 Czech (cs)
  - 🇩🇪 German (de)
  - 🇫🇷 French (fr)
  - 🇪🇸 Spanish (es)
- **Language Selector**: Dropdown in dashboard header for manual language selection
- **Browser Language Detection**: Automatically detects and uses browser's preferred language
- **Persistent Language Preference**: Cookie-based storage (1 year expiration)
- **Dynamic Language Discovery**: New languages automatically appear when JSON file added
- **474 Translated Strings**: All pages, dialogs, messages, and UI elements translated

### Changed
- Language selector uses JavaScript interop for cookie setting (Blazor Server compatibility)
- All 13 pages now support localization (Dashboard, Users, Profiles, Reports, etc.)

### Technical
- JSON-based translations in `wwwroot/localization/` (Docker-safe)
- LocalizationService with nested key support and fallback to English
- LanguageService with cookie management and browser detection
- Easy to extend: just add new `{lang}.json` file

## [1.4.2] - 2026-04-17

### Added
- **Ubuntu/Debian Package Support**: Native .deb package for easy installation
  - Build script: `scripts/build-deb.sh`
  - Post-install/pre-remove/post-remove scripts
  - Automatic systemd service configuration
  - GitHub Actions workflow for automatic package building on tags
  - Usage: `sudo dpkg -i parental-control-client_*.deb`

### Changed
- Updated README.md with .deb installation option
- Updated INSTALLATION.md with detailed .deb package instructions

## [1.4.1] - 2026-04-17

### Added
- **Automatic Linux Client Installation**: One-line installation script
  - Auto-detects system architecture (x64/arm64)
  - Downloads latest release from GitHub automatically
  - Configures systemd service
  - Interactive and non-interactive modes
  - Usage: `curl -fsSL https://raw.githubusercontent.com/crowndip/timekeeper-net/main/scripts/install-linux-client.sh | sudo bash -s -- http://server:8080`

### Changed
- Updated README.md with automatic installation as recommended option
- Updated INSTALLATION.md with simplified deployment instructions

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

[1.4.2]: https://github.com/crowndip/timekeeper-net/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/crowndip/timekeeper-net/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/crowndip/timekeeper-net/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/crowndip/timekeeper-net/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/crowndip/timekeeper-net/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/crowndip/timekeeper-net/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/crowndip/timekeeper-net/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/crowndip/timekeeper-net/releases/tag/v1.0.0

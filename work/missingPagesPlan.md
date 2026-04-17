# Missing Pages Implementation Plan

This document specifies the functionality and controls for the three placeholder pages that need to be implemented.

## 1. Users Management Page (`/users`)

### Purpose
Manage user accounts, configure their account types, and assign time profiles.

### Page Layout

#### Header Section
- **Title**: "👥 Users Management"
- **Add User Button**: Primary action button (top-right)

#### Users Table
Display all users in a data table with the following columns:

| Column | Description | Type |
|--------|-------------|------|
| Username | System username | Text (read-only) |
| Full Name | Display name | Text (editable) |
| Email | Contact email | Text (editable) |
| Account Type | Child/Parent/Technical | Dropdown |
| Is Active | Enable/disable user | Toggle switch |
| Actions | Edit, Delete buttons | Buttons |

#### Add/Edit User Dialog
Modal dialog with the following fields:

**Fields:**
- **Username** (required, max 64 chars)
  - Text input
  - Unique validation
  - Only lowercase letters, numbers, underscore, hyphen
  
- **Full Name** (optional, max 255 chars)
  - Text input
  
- **Email** (optional, max 255 chars)
  - Email input with validation
  
- **Account Type** (required)
  - Radio buttons or dropdown:
    - **Child**: Supervised with time limits
    - **Parent**: No limits, can be admin
    - **Technical**: System/service accounts, no limits
  
- **Is Active** (default: true)
  - Checkbox or toggle

**Buttons:**
- **Save**: Create/update user
- **Cancel**: Close dialog without saving

#### Delete Confirmation
Modal dialog:
- Warning message: "Are you sure you want to delete user [username]?"
- Note: "This will also delete all associated time profiles and usage records."
- **Delete** button (red/danger)
- **Cancel** button

### Functionality

**List Users:**
- Display all users from database
- Sort by username (default)
- Search/filter by username or full name

**Add User:**
- Validate all fields
- Check username uniqueness
- Create new user record
- Show success message
- Refresh user list

**Edit User:**
- Load existing user data
- Allow editing all fields except username
- Validate changes
- Update user record
- Show success message
- Refresh user list

**Delete User:**
- Show confirmation dialog
- Delete user and cascade delete related records
- Show success message
- Refresh user list

**Toggle Active Status:**
- Quick toggle without opening edit dialog
- Update database immediately
- Show success/error message

### API Endpoints Needed
- `GET /api/users` - List all users
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user
- `PATCH /api/users/{id}/active` - Toggle active status

---

## 2. Time Profiles Page (`/profiles`)

### Purpose
Configure time limits and allowed hours for users. Each user can have multiple profiles (e.g., "Weekday", "Weekend", "Summer").

### Page Layout

#### Header Section
- **Title**: "⏱️ Time Profiles"
- **Add Profile Button**: Primary action button (top-right)

#### User Selector
- **Dropdown**: Select user to view/manage their profiles
- Show only Child accounts (Parents and Technical accounts don't need profiles)
- Display: "Select a user to manage their time profiles"

#### Profiles List (for selected user)
Display profiles in cards or table:

**Card View (Recommended):**
Each profile card shows:
- **Profile Name** (e.g., "Weekday Schedule")
- **Status**: Active/Inactive badge
- **Daily Limits**: Summary (e.g., "Mon-Fri: 2h, Sat-Sun: 4h")
- **Allowed Hours**: Summary (e.g., "8:00 AM - 9:00 PM")
- **Actions**: Edit, Delete, Activate/Deactivate buttons

#### Add/Edit Profile Dialog
Modal dialog with tabs or sections:

**Section 1: Basic Information**
- **Profile Name** (required, max 100 chars)
  - Text input
  - Example: "School Days", "Weekend", "Summer Vacation"
  
- **Is Active** (default: true)
  - Toggle switch
  - Note: "Only one profile can be active per user at a time"

**Section 2: Daily Time Limits**
Configure minutes allowed per day:

| Day | Minutes Allowed |
|-----|-----------------|
| Monday | Number input (0-1440) |
| Tuesday | Number input (0-1440) |
| Wednesday | Number input (0-1440) |
| Thursday | Number input (0-1440) |
| Friday | Number input (0-1440) |
| Saturday | Number input (0-1440) |
| Sunday | Number input (0-1440) |

**Helper:**
- "Quick Set" buttons: 
  - "Weekdays: 2 hours" (Mon-Fri: 120 min)
  - "Weekends: 4 hours" (Sat-Sun: 240 min)
  - "All days: 3 hours" (All: 180 min)
  - "No limit" (All: 1440 min)

**Section 3: Allowed Hours**
Configure time windows when computer use is allowed:

For each day of the week:
- **Enabled**: Checkbox (allow restrictions for this day)
- **Start Time**: Time picker (e.g., 08:00)
- **End Time**: Time picker (e.g., 21:00)
- **Add Time Window**: Button to add multiple windows per day

**Example:**
```
Monday:
  ☑ Enabled
  08:00 - 12:00  [Remove]
  14:00 - 21:00  [Remove]
  [+ Add Time Window]
```

**Helper:**
- "Quick Set" buttons:
  - "School Days" (Mon-Fri: 15:00-21:00, Sat-Sun: 08:00-22:00)
  - "All Day" (All days: 00:00-23:59)
  - "Clear All"

**Buttons:**
- **Save**: Create/update profile
- **Cancel**: Close dialog without saving

#### Delete Confirmation
Modal dialog:
- Warning: "Are you sure you want to delete profile '[name]'?"
- Note: "Usage history will be preserved."
- **Delete** button (red/danger)
- **Cancel** button

### Functionality

**Select User:**
- Load user's profiles when selected
- Show message if user has no profiles
- Show "Add Profile" button

**List Profiles:**
- Display all profiles for selected user
- Highlight active profile
- Show summary of limits and hours

**Add Profile:**
- Validate all fields
- Check if activating (deactivate other profiles if needed)
- Create new profile record
- Create allowed hours records
- Show success message
- Refresh profile list

**Edit Profile:**
- Load existing profile data
- Load allowed hours
- Allow editing all fields
- Validate changes
- Update profile and allowed hours
- Handle active status (deactivate others if needed)
- Show success message
- Refresh profile list

**Delete Profile:**
- Show confirmation dialog
- Delete profile and cascade delete allowed hours
- Show success message
- Refresh profile list

**Activate/Deactivate Profile:**
- Quick toggle without opening edit dialog
- Deactivate other profiles if activating
- Update database immediately
- Show success/error message

### API Endpoints Needed
- `GET /api/users?accountType=Child` - List child users for dropdown
- `GET /api/profiles?userId={id}` - List profiles for user
- `POST /api/profiles` - Create new profile
- `PUT /api/profiles/{id}` - Update profile
- `DELETE /api/profiles/{id}` - Delete profile
- `PATCH /api/profiles/{id}/activate` - Activate profile (deactivate others)
- `GET /api/profiles/{id}/allowed-hours` - Get allowed hours for profile
- `POST /api/profiles/{id}/allowed-hours` - Create allowed hours
- `PUT /api/allowed-hours/{id}` - Update allowed hours
- `DELETE /api/allowed-hours/{id}` - Delete allowed hours

---

## 3. Computers/Devices Page (`/computers`)

### Purpose
View and manage registered client devices (computers). Monitor their status and manage API keys.

### Page Layout

#### Header Section
- **Title**: "💻 Registered Devices"
- **Refresh Button**: Reload device list

#### Devices Table
Display all registered computers in a data table:

| Column | Description | Type |
|--------|-------------|------|
| Hostname | Computer name | Text (read-only) |
| Machine ID | Unique identifier | Text (read-only, truncated) |
| OS Info | Operating system | Text (read-only) |
| Last Seen | Last heartbeat time | DateTime (relative, e.g., "5 minutes ago") |
| Status | Online/Offline | Badge (green/gray) |
| Is Active | Enable/disable device | Toggle switch |
| Actions | View Details, Regenerate Key, Delete | Buttons |

**Status Logic:**
- **Online**: Last seen within 5 minutes (green badge)
- **Offline**: Last seen > 5 minutes ago (gray badge)

#### Device Details Dialog
Modal dialog showing full device information:

**Information:**
- **Hostname**: Full hostname
- **Machine ID**: Full machine ID (with copy button)
- **OS Info**: Full OS information
- **API Key**: Masked (••••••••) with "Show" and "Copy" buttons
- **Created At**: Registration date/time
- **Updated At**: Last update date/time
- **Last Seen At**: Last heartbeat date/time
- **Is Active**: Current status

**Buttons:**
- **Regenerate API Key**: Generate new key (with confirmation)
- **Close**: Close dialog

#### Regenerate API Key Confirmation
Modal dialog:
- Warning: "Are you sure you want to regenerate the API key for '[hostname]'?"
- Note: "The client will need to be reconfigured with the new key."
- **New API Key Display**: Show new key after generation (with copy button)
- **Regenerate** button (warning color)
- **Cancel** button

#### Delete Confirmation
Modal dialog:
- Warning: "Are you sure you want to delete device '[hostname]'?"
- Note: "This will also delete all associated sessions and usage records."
- **Delete** button (red/danger)
- **Cancel** button

### Functionality

**List Devices:**
- Display all computers from database
- Sort by last seen (most recent first)
- Show online/offline status
- Auto-refresh every 30 seconds (optional)

**View Details:**
- Open modal with full device information
- Show masked API key
- Allow showing/copying API key
- Display all timestamps

**Regenerate API Key:**
- Show confirmation dialog
- Generate new random API key
- Update database
- Display new key (one-time view)
- Show success message
- Refresh device list

**Delete Device:**
- Show confirmation dialog
- Delete device and cascade delete related records
- Show success message
- Refresh device list

**Toggle Active Status:**
- Quick toggle without opening details dialog
- Update database immediately
- Show success/error message
- Inactive devices cannot connect

**Auto-Refresh:**
- Optional: Refresh device list every 30 seconds
- Update "Last Seen" times
- Update online/offline status
- Toggle button to enable/disable auto-refresh

### API Endpoints Needed
- `GET /api/computers` - List all computers
- `GET /api/computers/{id}` - Get computer details
- `DELETE /api/computers/{id}` - Delete computer
- `PATCH /api/computers/{id}/active` - Toggle active status
- `POST /api/computers/{id}/regenerate-key` - Regenerate API key

---

## Common UI Components

### Design System
Use consistent styling across all pages:

**Colors:**
- Primary: `#667eea` (purple gradient)
- Success: `#10b981` (green)
- Warning: `#f59e0b` (orange)
- Danger: `#ef4444` (red)
- Gray: `#6b7280` (neutral)

**Buttons:**
- Primary: Gradient background, white text
- Secondary: Gray background, white text
- Danger: Red background, white text
- Icon buttons: Transparent with hover effect

**Tables:**
- Striped rows for readability
- Hover effect on rows
- Responsive (stack on mobile)
- Sortable columns
- Search/filter input

**Modals:**
- Centered overlay
- White background with shadow
- Close button (X) in top-right
- Backdrop click to close (optional)
- Escape key to close

**Forms:**
- Clear labels above inputs
- Validation messages below inputs
- Required field indicators (*)
- Disabled state styling
- Focus states

**Notifications:**
- Toast messages (top-right)
- Auto-dismiss after 3-5 seconds
- Success (green), Error (red), Info (blue), Warning (orange)

### Validation Rules

**Username:**
- Required
- 3-64 characters
- Lowercase letters, numbers, underscore, hyphen only
- Must be unique

**Email:**
- Optional
- Valid email format
- Max 255 characters

**Profile Name:**
- Required
- 1-100 characters
- Must be unique per user

**Time Limits:**
- 0-1440 minutes (0-24 hours)
- Integer only

**Allowed Hours:**
- Start time must be before end time
- No overlapping time windows for same day
- Time format: HH:mm (24-hour)

### Error Handling

**Display Errors:**
- Show validation errors inline (below field)
- Show API errors in toast notifications
- Show detailed error messages (not just "Error occurred")

**Common Errors:**
- Network errors: "Unable to connect to server"
- Validation errors: "Username must be 3-64 characters"
- Duplicate errors: "Username already exists"
- Not found errors: "User not found"
- Permission errors: "You don't have permission to perform this action"

### Loading States

**Show Loading Indicators:**
- Spinner in button during save/delete operations
- Skeleton loaders for tables while loading data
- Disabled state for forms during submission
- Progress bar for long operations

### Accessibility

**Requirements:**
- Keyboard navigation support
- ARIA labels for screen readers
- Focus indicators
- Color contrast compliance (WCAG AA)
- Form field associations (label + input)

---

## Implementation Priority

### Phase 1: Users Management (Highest Priority)
Users are the foundation - needed before profiles can be created.

**Tasks:**
1. Create API endpoints for user CRUD operations
2. Implement Users page with table and dialogs
3. Add validation and error handling
4. Test all operations

### Phase 2: Time Profiles (High Priority)
Profiles define the time limits for users.

**Tasks:**
1. Create API endpoints for profile and allowed hours CRUD
2. Implement Profiles page with user selector
3. Implement profile cards/table
4. Implement add/edit dialog with tabs
5. Add validation and error handling
6. Test all operations

### Phase 3: Computers/Devices (Medium Priority)
Devices are registered automatically by clients, but management is useful.

**Tasks:**
1. Create API endpoints for computer management
2. Implement Computers page with table
3. Implement details dialog
4. Implement API key regeneration
5. Add auto-refresh functionality
6. Test all operations

---

## Technical Notes

### Database Schema
All required tables already exist:
- `Users` - User accounts
- `TimeProfiles` - Time limit profiles
- `AllowedHours` - Allowed time windows
- `Computers` - Registered devices
- `Sessions` - Active sessions
- `TimeUsage` - Usage tracking
- `TimeAdjustments` - Manual adjustments

### API Structure
Follow RESTful conventions:
- `GET` - Retrieve data
- `POST` - Create new records
- `PUT` - Update entire record
- `PATCH` - Update specific fields
- `DELETE` - Delete records

### Response Format
```json
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully"
}
```

Error format:
```json
{
  "success": false,
  "error": "Error message",
  "details": { ... }
}
```

### Security Considerations
- Validate all inputs server-side
- Sanitize user inputs
- Use parameterized queries (EF Core handles this)
- Implement rate limiting for API endpoints
- Log all administrative actions
- Require authentication for all operations (future)

---

## Testing Checklist

### Users Page
- [ ] List all users
- [ ] Add new user with valid data
- [ ] Add user with duplicate username (should fail)
- [ ] Edit user information
- [ ] Delete user (with confirmation)
- [ ] Toggle user active status
- [ ] Search/filter users
- [ ] Validate all form fields

### Profiles Page
- [ ] Select user from dropdown
- [ ] List user's profiles
- [ ] Add new profile with valid data
- [ ] Add profile with duplicate name (should fail)
- [ ] Edit profile information
- [ ] Delete profile (with confirmation)
- [ ] Activate profile (deactivates others)
- [ ] Add/edit/delete allowed hours
- [ ] Validate time limits (0-1440)
- [ ] Validate time windows (no overlap)

### Computers Page
- [ ] List all computers
- [ ] View computer details
- [ ] Regenerate API key
- [ ] Copy API key to clipboard
- [ ] Delete computer (with confirmation)
- [ ] Toggle computer active status
- [ ] Auto-refresh device list
- [ ] Show online/offline status correctly

---

## Future Enhancements

### Users Page
- Bulk operations (activate/deactivate multiple users)
- Import users from CSV
- Export users to CSV
- User groups/categories
- Profile picture upload

### Profiles Page
- Clone profile to another user
- Profile templates (predefined profiles)
- Bulk apply profile to multiple users
- Schedule profile changes (e.g., switch to "Summer" on June 1)
- Temporary time adjustments (add/subtract time)

### Computers Page
- Device groups/locations
- Remote commands (lock, logout)
- Device health monitoring
- Client version tracking
- Update notifications

---

## Conclusion

This plan provides detailed specifications for implementing the three missing pages. Each page follows consistent design patterns and includes all necessary functionality for a complete parental control system.

**Estimated Implementation Time:**
- Users Page: 8-12 hours
- Profiles Page: 16-20 hours (most complex)
- Computers Page: 6-8 hours

**Total: 30-40 hours of development time**

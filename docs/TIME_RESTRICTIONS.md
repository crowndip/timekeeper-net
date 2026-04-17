# Time-of-Day Restrictions

## Overview

The system now supports time-of-day restrictions (e.g., no computer before 8 AM or after 10 PM).

## How It Works

1. **Database**: `AllowedHours` table stores time restrictions per profile
2. **Enforcement**: Client checks both:
   - Time remaining (daily/weekly limits)
   - Current time is within allowed hours
3. **Default**: If no allowed hours configured, all times are allowed

## Configuration

### Via Database (SQL)

```sql
-- Add allowed hours for a profile (Monday-Friday, 8 AM to 10 PM)
INSERT INTO "AllowedHours" ("Id", "ProfileId", "DayOfWeek", "StartTime", "EndTime")
VALUES 
  (gen_random_uuid(), 'profile-guid-here', 1, '08:00:00', '22:00:00'), -- Monday
  (gen_random_uuid(), 'profile-guid-here', 2, '08:00:00', '22:00:00'), -- Tuesday
  (gen_random_uuid(), 'profile-guid-here', 3, '08:00:00', '22:00:00'), -- Wednesday
  (gen_random_uuid(), 'profile-guid-here', 4, '08:00:00', '22:00:00'), -- Thursday
  (gen_random_uuid(), 'profile-guid-here', 5, '08:00:00', '22:00:00'); -- Friday

-- Weekend: 9 AM to 11 PM
INSERT INTO "AllowedHours" ("Id", "ProfileId", "DayOfWeek", "StartTime", "EndTime")
VALUES 
  (gen_random_uuid(), 'profile-guid-here', 6, '09:00:00', '23:00:00'), -- Saturday
  (gen_random_uuid(), 'profile-guid-here', 0, '09:00:00', '23:00:00'); -- Sunday
```

### Day of Week Values
- 0 = Sunday
- 1 = Monday
- 2 = Tuesday
- 3 = Wednesday
- 4 = Thursday
- 5 = Friday
- 6 = Saturday

## Example Scenarios

### Scenario 1: School Nights
- Monday-Thursday: 4 PM to 9 PM
- Friday: 4 PM to 11 PM
- Saturday-Sunday: 9 AM to 11 PM

### Scenario 2: No Computer Before School
- Monday-Friday: 3 PM to 10 PM (after school only)
- Saturday-Sunday: All day (8 AM to 10 PM)

### Scenario 3: Homework Time Protected
- Monday-Friday: 
  - Morning: 7 AM to 8 AM
  - After school: 4 PM to 6 PM (homework)
  - Evening: 7 PM to 9 PM (free time)

## Enforcement

When a child tries to use the computer outside allowed hours:
1. Client checks with server
2. Server returns `ShouldEnforce: true`
3. Client logs out the user immediately

## Database Migrations

EF Core automatically applies migrations on server startup. No manual intervention needed.

## Future UI

A web UI for configuring allowed hours will be added in a future release. For now, use SQL or API.

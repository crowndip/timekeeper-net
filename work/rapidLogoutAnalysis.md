# Client Log Analysis - Rapid Logout Issue

## Problem Summary
User "vasik" is being logged out immediately and repeatedly after time was extended, even though he should have time remaining.

## Key Observations

### 1. **Rapid Logout Loop**
Every 60 seconds (tick interval), the client:
- Sends usage report
- Receives response
- **Immediately enforces logout**
- User logs back in
- Cycle repeats

### 2. **Session Changes**
Notice the session IDs changing rapidly:
- Session 3 → Session 7 → Session 12 → Session 16 → Session 20 → Session 24

This means the user is being logged out, logging back in, getting logged out again immediately.

### 3. **Time Extension Detected**
At 08:55:57, the log shows:
```
[INF] Time increased from -5 to 0 minutes, resetting warnings
```

This confirms:
- User had **-5 minutes** (overdrawn)
- Parent extended time to bring it to **0 minutes**
- But 0 minutes still triggers enforcement!

### 4. **The Root Cause**

The server is returning `ShouldEnforce = true` even when time remaining is 0 or positive.

**Problem**: The enforcement logic is checking `timeRemaining <= 0` instead of `timeRemaining < 0`.

When time is exactly 0, it should NOT enforce yet - the user should be able to use that last minute.

## The Bug

Location: Server-side enforcement logic

Current logic (incorrect):
```csharp
if (timeRemaining <= 0)  // BUG: Enforces at 0 minutes
{
    shouldEnforce = true;
}
```

Should be:
```csharp
if (timeRemaining < 0)  // Correct: Only enforce when negative
{
    shouldEnforce = true;
}
```

## Why Minutes Count Faster

The "faster counting" is an illusion caused by:
1. User logs in
2. Client sends usage report (adds 1 minute)
3. Server calculates: 0 - 1 = -1 minutes
4. Server returns `ShouldEnforce = true`
5. Client logs user out immediately
6. User logs back in (new session)
7. Repeat

So every login attempt consumes 1 minute instantly because the client sends a usage report immediately, and the server enforces immediately.

## Additional Issues

### Multiple Usage Reports Per Tick
The logs show **2 usage reports** being sent per tick:
```
08:52:56.837 - First POST /api/client/usage
08:52:56.929 - Second POST /api/client/usage (92ms later)
```

This suggests the client is:
1. Sending usage for the active session
2. Sending another check immediately after

This could be causing double-counting of minutes.

## Solution

### Fix 1: Server Enforcement Logic
Change the enforcement threshold to only enforce when time is **negative**, not zero.

### Fix 2: Client Behavior After Enforcement
After enforcing logout, the client should:
- Mark the session as "enforced"
- Not immediately re-check on the next tick
- Wait for the user to actually log out before processing new sessions

### Fix 3: Prevent Double Usage Reports
Ensure only one usage report is sent per tick per user.

## Files to Check

1. **Server**: `src/ParentalControl.WebService/Services/TimeCalculationService.cs`
   - Method: `ShouldEnforceAsync`
   - Check the condition for enforcement

2. **Client**: `src/ParentalControl.Client/ParentalControlWorker.cs`
   - Method: `ProcessTickAsync`
   - Check why two usage reports are sent
   - Check enforcement behavior

3. **Client**: `src/ParentalControl.Client/Services/EnforcementEngine.cs`
   - Check if enforcement is being triggered multiple times
   - Check if there's a cooldown after enforcement

# Root Cause Found

## The Data
- **Thursday Limit**: 60 minutes
- **Minutes Used Today**: 26 minutes  
- **Time Adjustment**: NULL (no adjustment recorded!)
- **Weekly Limit**: 0 (unlimited)

## Expected Calculation
`timeRemaining = 60 - 26 + 0 = 34 minutes`

Vasik should have **34 minutes remaining**.

## Why Server Returns 0

The time adjustment you made **was not saved to the database**!

When you "extended the time allocation for Thursday", it either:
1. Didn't save to the TimeAdjustments table
2. Was saved for a different user
3. Was saved for a different date
4. The UI didn't actually submit the adjustment

## Verification Queries

Run these to check if adjustment exists anywhere:

```sql
-- Check all adjustments for vasik (any date)
SELECT 
    "AdjustmentDate",
    "MinutesAdjustment",
    "Reason",
    "CreatedAt"
FROM "TimeAdjustments"
WHERE "UserId" = '8399b335-cd0b-48ca-b1db-405d94d616d3'
ORDER BY "CreatedAt" DESC;

-- Check all adjustments for today (any user)
SELECT 
    u."Username",
    ta."MinutesAdjustment",
    ta."Reason",
    ta."CreatedAt"
FROM "TimeAdjustments" ta
JOIN "Users" u ON u."Id" = ta."UserId"
WHERE ta."AdjustmentDate" = '2026-04-30'
ORDER BY ta."CreatedAt" DESC;
```

## The Real Issue

The log shows:
```
[INF] Time increased from -5 to 0 minutes, resetting warnings
```

This suggests the client DID receive a time increase at some point, but:
1. It went from -5 to 0 (not to 34)
2. This means the adjustment was only 5 minutes, not enough

## Possible Scenarios

### Scenario 1: Adjustment Not Saved
You tried to add time but the UI didn't save it to the database.

### Scenario 2: Wrong Amount
You added 5 minutes instead of more, bringing -5 to 0.

### Scenario 3: Timing Issue
The adjustment was made AFTER the client already used more time, so by the time it was applied, it was already consumed.

## Why Rapid Logout Happens

With 34 minutes remaining, vasik should NOT be logged out. But the server is returning 0 or negative, causing immediate logout.

**Most likely**: The server is calculating based on stale data or there's a bug in the calculation that's not accounting for something.

## Next Steps

1. Check if there are ANY time adjustments for vasik
2. Manually add a time adjustment and verify it saves
3. Check if there's a caching issue in the server
4. Verify the calculation logic is using the correct date/time

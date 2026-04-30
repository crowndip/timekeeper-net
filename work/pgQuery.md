

-- Query 1: Get vasik's profile and limits
SELECT 
    u."Username",
    p."ThursdayLimit",
    p."WeeklyLimit",
    p."IsActive"
FROM "Users" u
LEFT JOIN "TimeProfiles" p ON p."UserId" = u."Id" AND p."IsActive" = true
WHERE u."Username" = 'vasik';

vasik   60  0   true


-- Query 2: Get vasik's usage today
SELECT 
    SUM(tu."MinutesUsed") as total_minutes_used
FROM "TimeUsage" tu
WHERE tu."UserId" = '8399b335-cd0b-48ca-b1db-405d94d616d3'
AND tu."UsageDate" = '2026-04-30';

26

-- Query 3: Get vasik's time adjustments today
SELECT 
    SUM("MinutesAdjustment") as total_adjustment
FROM "TimeAdjustments"
WHERE "UserId" = '8399b335-cd0b-48ca-b1db-405d94d616d3'
AND "AdjustmentDate" = '2026-04-30';

nullp
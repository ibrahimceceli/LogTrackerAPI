-- Aggregate reports used by /api/reports. Simple paged lists are plain SELECTs in the application.
-- Every window is half-open: OccurredAtUtc >= @FromUtc AND OccurredAtUtc < @ToUtc.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.GetRequestSummary
    @FromUtc datetime2(3),
    @ToUtc   datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(*)                                                    AS TotalRequests,
        COUNT(CASE WHEN StatusCode >= 500 THEN 1 END)               AS ServerErrorCount,
        COUNT(CASE WHEN StatusCode BETWEEN 400 AND 499 THEN 1 END)  AS ClientErrorCount,
        -- Cast before averaging: AVG over an int column returns a truncated int.
        ROUND(AVG(CAST(DurationMs AS float)), 2)                    AS AverageDurationMs,
        -- PERCENTILE_CONT is a window function, so it repeats on every row; MAX just picks that value.
        ROUND(MAX(P95DurationMs), 2)                                AS P95DurationMs
    FROM
    (
        SELECT
            StatusCode,
            DurationMs,
            PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY DurationMs) OVER () AS P95DurationMs
        FROM dbo.RequestLogs
        WHERE OccurredAtUtc >= @FromUtc AND OccurredAtUtc < @ToUtc
    ) AS Requests;
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetEndpointStats
    @FromUtc datetime2(3),
    @ToUtc   datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Method,
        Endpoint,
        COUNT(*)                                       AS TotalRequests,
        COUNT(CASE WHEN StatusCode >= 500 THEN 1 END)  AS ServerErrorCount,
        ROUND(AVG(CAST(DurationMs AS float)), 2)       AS AverageDurationMs,
        ROUND(MAX(P95DurationMs), 2)                   AS P95DurationMs,
        MAX(DurationMs)                                AS MaxDurationMs
    FROM
    (
        SELECT
            Method,
            Endpoint,
            StatusCode,
            DurationMs,
            PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY DurationMs) OVER (PARTITION BY Method, Endpoint) AS P95DurationMs
        FROM dbo.RequestLogs
        WHERE OccurredAtUtc >= @FromUtc AND OccurredAtUtc < @ToUtc
    ) AS Requests
    GROUP BY Method, Endpoint
    ORDER BY TotalRequests DESC, Endpoint, Method;
END;
GO

-- Long format (one row per hour and error type) instead of a PIVOT with hard-coded columns,
-- so new exception types show up without changing SQL.
CREATE OR ALTER PROCEDURE dbo.GetHourlyErrorCounts
    @FromUtc datetime2(3),
    @ToUtc   datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    -- Rounds down to the hour without DATETRUNC, which needs SQL Server 2022.
    DECLARE @Origin datetime2(3) = '2000-01-01T00:00:00';

    SELECT
        HourStartUtc,
        ErrorType,
        COUNT(*) AS [Count]
    FROM
    (
        SELECT
            DATEADD(hour, DATEDIFF(hour, @Origin, OccurredAtUtc), @Origin) AS HourStartUtc,
            ErrorType
        FROM dbo.ErrorEvents
        WHERE OccurredAtUtc >= @FromUtc AND OccurredAtUtc < @ToUtc
    ) AS Errors
    GROUP BY HourStartUtc, ErrorType
    ORDER BY HourStartUtc, ErrorType;
END;
GO

-- Average duration per endpoint and weekday over exactly the last 7 UTC calendar days, today included.
CREATE OR ALTER PROCEDURE dbo.GetWeeklyTrend
    @NowUtc datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today   date         = CAST(COALESCE(@NowUtc, SYSUTCDATETIME()) AS date);
    DECLARE @FromUtc datetime2(3) = DATEADD(day, -6, CAST(@Today AS datetime2(3)));
    DECLARE @ToUtc   datetime2(3) = DATEADD(day, 1, CAST(@Today AS datetime2(3)));

    -- DATENAME and DATEPART(weekday) change with SET LANGUAGE and SET DATEFIRST.
    -- Counting days since a known Monday gives 0 = Monday ... 6 = Sunday on every server.
    DECLARE @KnownMonday date = '19000101';

    SELECT
        Method,
        Endpoint,
        ROUND([0], 2) AS Monday,
        ROUND([1], 2) AS Tuesday,
        ROUND([2], 2) AS Wednesday,
        ROUND([3], 2) AS Thursday,
        ROUND([4], 2) AS Friday,
        ROUND([5], 2) AS Saturday,
        ROUND([6], 2) AS Sunday
    FROM
    (
        SELECT
            Method,
            Endpoint,
            DATEDIFF(day, @KnownMonday, CAST(OccurredAtUtc AS date)) % 7 AS DayIndex,
            CAST(DurationMs AS float) AS DurationMs
        FROM dbo.RequestLogs
        WHERE OccurredAtUtc >= @FromUtc AND OccurredAtUtc < @ToUtc
    ) AS DailyDurations
    PIVOT
    (
        AVG(DurationMs) FOR DayIndex IN ([0], [1], [2], [3], [4], [5], [6])
    ) AS ByWeekday
    ORDER BY Endpoint, Method;
END;
GO

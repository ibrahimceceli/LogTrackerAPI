-- Optional demo data: about a week of traffic for the demo API, so the dashboard has something to show.
-- Inserts only when both tables are empty, so it never mixes with real data.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RequestLogs) AND NOT EXISTS (SELECT 1 FROM dbo.ErrorEvents)
BEGIN
    DECLARE @NowUtc       datetime2(3) = SYSUTCDATETIME();
    DECLARE @RequestCount int          = 4000;

    -- Demo routes with a relative weight, a duration range and, for failing routes, the exception they throw.
    DECLARE @Routes TABLE
    (
        RouteId      int            NOT NULL PRIMARY KEY,
        Method       nvarchar(16)   NOT NULL,
        Endpoint     nvarchar(256)  NOT NULL,
        Path         nvarchar(512)  NOT NULL,
        StatusCode   smallint       NOT NULL,
        MinMs        int            NOT NULL,
        MaxMs        int            NOT NULL,
        Weight       int            NOT NULL,
        ErrorType    nvarchar(256)  NULL,
        ErrorMessage nvarchar(4000) NULL
    );

    INSERT INTO @Routes (RouteId, Method, Endpoint, Path, StatusCode, MinMs, MaxMs, Weight, ErrorType, ErrorMessage)
    VALUES
        (1,  N'GET',  N'/api/products',           N'/api/products',                      200,  10,   80, 30, NULL, NULL),
        (2,  N'GET',  N'/api/products/{id:int}',  N'/api/products/7',                    200,  10,   80, 18, NULL, NULL),
        (3,  N'GET',  N'/api/products/{id:int}',  N'/api/products/42',                   404,   8,   40,  3, NULL, NULL),
        (4,  N'POST', N'/api/orders',             N'/api/orders',                        201,  30,  200, 14, NULL, NULL),
        (5,  N'POST', N'/api/orders',             N'/api/orders',                        400,  30,  120,  3, NULL, NULL),
        (6,  N'GET',  N'/api/orders/{id:int}',    N'/api/orders/3',                      200,   1,   12,  8, NULL, NULL),
        (7,  N'GET',  N'/api/orders/{id:int}',    N'/api/orders/999',                    404,   1,    8,  2, NULL, NULL),
        (8,  N'GET',  N'/api/test/slow',          N'/api/test/slow',                     200, 900, 2500,  3, NULL, NULL),
        (9,  N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/null-reference',    500,   2,   30,  3, N'System.NullReferenceException',    N'Simulated null reference.'),
        (10, N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/invalid-operation', 500,   2,   30,  3, N'System.InvalidOperationException', N'Simulated invalid operation.'),
        (11, N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/divide-by-zero',    500,   2,   30,  2, N'System.DivideByZeroException',     N'Simulated division by zero.'),
        (12, N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/timeout',           500,   2,   30,  2, N'System.TimeoutException',          N'Simulated timeout.'),
        (13, N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/argument',          500,   2,   30,  1, N'System.ArgumentException',         N'Simulated invalid argument.'),
        (14, N'GET',  N'/api/test/errors/{type}', N'/api/test/errors/key-not-found',     500,   2,   30,  1, N'System.Collections.Generic.KeyNotFoundException', N'Simulated missing key.'),
        (15, N'GET',  N'(unmatched)',             N'/wp-login.php',                      404,   1,    5,  2, NULL, NULL);

    -- One slot per unit of weight, so a random slot number picks a route in proportion to its weight.
    DECLARE @Slots TABLE (SlotNumber int NOT NULL PRIMARY KEY, RouteId int NOT NULL);

    INSERT INTO @Slots (SlotNumber, RouteId)
    SELECT ROW_NUMBER() OVER (ORDER BY r.RouteId, w.N), r.RouteId
    FROM @Routes AS r
    JOIN (SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N FROM sys.all_objects) AS w
        ON w.N <= r.Weight;

    DECLARE @SlotCount int = (SELECT COUNT(*) FROM @Slots);

    -- The random values are stored in a temp table first: inside a CTE or join,
    -- SQL Server may evaluate NEWID() more than once for the same row.
    SELECT TOP (@RequestCount)
        DATEADD(millisecond, -(ABS(CHECKSUM(NEWID())) % 604800000), @NowUtc) AS OccurredAtUtc, -- last 7 days
        1 + ABS(CHECKSUM(NEWID())) % @SlotCount                               AS SlotNumber,
        ABS(CHECKSUM(NEWID()))                                                AS DurationSeed,
        CONCAT(N'00-', LOWER(REPLACE(CONVERT(nchar(36), NEWID()), N'-', N'')),
               N'-', LEFT(LOWER(REPLACE(CONVERT(nchar(36), NEWID()), N'-', N'')), 16),
               N'-00')                                                        AS TraceId
    INTO #SeedRequests
    FROM sys.all_objects AS a
    CROSS JOIN sys.all_objects AS b;

    INSERT INTO dbo.RequestLogs (OccurredAtUtc, Method, Endpoint, Path, StatusCode, DurationMs, TraceId)
    SELECT q.OccurredAtUtc, r.Method, r.Endpoint, r.Path, r.StatusCode,
           r.MinMs + q.DurationSeed % (r.MaxMs - r.MinMs + 1),
           q.TraceId
    FROM #SeedRequests AS q
    JOIN @Slots  AS s ON s.SlotNumber = q.SlotNumber
    JOIN @Routes AS r ON r.RouteId = s.RouteId;

    -- Every failed test request gets a matching error event with the same time and trace id.
    INSERT INTO dbo.ErrorEvents (OccurredAtUtc, ErrorType, Message, Source, StackTrace, Method, Path, Endpoint, StatusCode, TraceId)
    SELECT l.OccurredAtUtc, r.ErrorType, r.ErrorMessage,
           N'LogTracker.Api.Endpoints.TestEndpoints',
           CONCAT(r.ErrorType, N': ', r.ErrorMessage, NCHAR(13), NCHAR(10),
                  N'   at LogTracker.Api.Endpoints.TestEndpoints.ThrowError(String type)'),
           l.Method, l.Path, l.Endpoint, l.StatusCode, l.TraceId
    FROM dbo.RequestLogs AS l
    JOIN @Routes AS r ON r.Path = l.Path AND r.StatusCode = l.StatusCode
    WHERE r.ErrorType IS NOT NULL;

    DROP TABLE #SeedRequests;
END;
GO

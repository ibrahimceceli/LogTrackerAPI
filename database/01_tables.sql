-- LogTracker tables. Safe to run more than once.
-- Times are stored in UTC (datetime2 + SYSUTCDATETIME) so reports do not depend on the server's time zone.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RequestLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RequestLogs
    (
        Id            bigint IDENTITY(1, 1) NOT NULL,
        OccurredAtUtc datetime2(3)  NOT NULL CONSTRAINT DF_RequestLogs_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
        Method        nvarchar(16)  NOT NULL,
        Endpoint      nvarchar(256) NOT NULL, -- route template, e.g. /api/products/{id:int}, or (unmatched)
        Path          nvarchar(512) NOT NULL, -- request path without the query string
        StatusCode    smallint      NOT NULL,
        DurationMs    int           NOT NULL,
        TraceId       nvarchar(128) NULL,
        CONSTRAINT PK_RequestLogs PRIMARY KEY CLUSTERED (Id)
    );
END;
GO

-- Every report filters on a time window; the included columns let them run without key lookups.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RequestLogs_OccurredAtUtc' AND object_id = OBJECT_ID(N'dbo.RequestLogs'))
    CREATE NONCLUSTERED INDEX IX_RequestLogs_OccurredAtUtc
        ON dbo.RequestLogs (OccurredAtUtc)
        INCLUDE (Method, Endpoint, StatusCode, DurationMs);
GO

IF OBJECT_ID(N'dbo.ErrorEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorEvents
    (
        Id            bigint IDENTITY(1, 1) NOT NULL,
        OccurredAtUtc datetime2(3)   NOT NULL CONSTRAINT DF_ErrorEvents_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
        ErrorType     nvarchar(256)  NOT NULL, -- exception type name, e.g. System.TimeoutException
        Message       nvarchar(4000) NOT NULL,
        Source        nvarchar(512)  NULL,
        StackTrace    nvarchar(max)  NULL,     -- full exception text, including inner exceptions
        Method        nvarchar(16)   NOT NULL,
        Path          nvarchar(512)  NOT NULL,
        Endpoint      nvarchar(256)  NOT NULL,
        StatusCode    smallint       NOT NULL,
        TraceId       nvarchar(128)  NULL,
        CONSTRAINT PK_ErrorEvents PRIMARY KEY CLUSTERED (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErrorEvents_OccurredAtUtc' AND object_id = OBJECT_ID(N'dbo.ErrorEvents'))
    CREATE NONCLUSTERED INDEX IX_ErrorEvents_OccurredAtUtc
        ON dbo.ErrorEvents (OccurredAtUtc)
        INCLUDE (ErrorType);
GO

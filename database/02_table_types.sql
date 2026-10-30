-- Table-valued parameter types used to insert a whole batch of log entries in one round trip.
-- A table type cannot be altered while procedures use it, so it is only created when missing.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF TYPE_ID(N'dbo.RequestLogTableType') IS NULL
    CREATE TYPE dbo.RequestLogTableType AS TABLE
    (
        OccurredAtUtc datetime2(3)  NOT NULL,
        Method        nvarchar(16)  NOT NULL,
        Endpoint      nvarchar(256) NOT NULL,
        Path          nvarchar(512) NOT NULL,
        StatusCode    smallint      NOT NULL,
        DurationMs    int           NOT NULL,
        TraceId       nvarchar(128) NULL
    );
GO

IF TYPE_ID(N'dbo.ErrorEventTableType') IS NULL
    CREATE TYPE dbo.ErrorEventTableType AS TABLE
    (
        OccurredAtUtc datetime2(3)   NOT NULL,
        ErrorType     nvarchar(256)  NOT NULL,
        Message       nvarchar(4000) NOT NULL,
        Source        nvarchar(512)  NULL,
        StackTrace    nvarchar(max)  NULL,
        Method        nvarchar(16)   NOT NULL,
        Path          nvarchar(512)  NOT NULL,
        Endpoint      nvarchar(256)  NOT NULL,
        StatusCode    smallint       NOT NULL,
        TraceId       nvarchar(128)  NULL
    );
GO

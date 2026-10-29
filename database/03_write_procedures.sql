-- Batch insert procedures called by the background log writer.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.InsertRequestLogs
    @Entries dbo.RequestLogTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.RequestLogs (OccurredAtUtc, Method, Endpoint, Path, StatusCode, DurationMs, TraceId)
    SELECT OccurredAtUtc, Method, Endpoint, Path, StatusCode, DurationMs, TraceId
    FROM @Entries;
END;
GO

CREATE OR ALTER PROCEDURE dbo.InsertErrorEvents
    @Entries dbo.ErrorEventTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ErrorEvents (OccurredAtUtc, ErrorType, Message, Source, StackTrace, Method, Path, Endpoint, StatusCode, TraceId)
    SELECT OccurredAtUtc, ErrorType, Message, Source, StackTrace, Method, Path, Endpoint, StatusCode, TraceId
    FROM @Entries;
END;
GO

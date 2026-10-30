-- Deletes log rows older than @RetentionDays. Called once a day by the application.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.PurgeOldLogs
    @RetentionDays int,
    @BatchSize     int = 5000
AS
BEGIN
    SET NOCOUNT ON;

    IF @RetentionDays IS NULL OR @RetentionDays < 1
        THROW 50000, N'@RetentionDays must be at least 1.', 1;

    DECLARE @CutoffUtc          datetime2(3) = DATEADD(day, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @Deleted            int;
    DECLARE @DeletedRequestLogs int = 0;
    DECLARE @DeletedErrorEvents int = 0;

    -- Small batches keep each transaction short, so a large purge does not block the log writer.
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@BatchSize) FROM dbo.RequestLogs WHERE OccurredAtUtc < @CutoffUtc;
        SET @Deleted = @@ROWCOUNT;
        SET @DeletedRequestLogs += @Deleted;
        IF @Deleted < @BatchSize BREAK;
    END;

    WHILE 1 = 1
    BEGIN
        DELETE TOP (@BatchSize) FROM dbo.ErrorEvents WHERE OccurredAtUtc < @CutoffUtc;
        SET @Deleted = @@ROWCOUNT;
        SET @DeletedErrorEvents += @Deleted;
        IF @Deleted < @BatchSize BREAK;
    END;

    SELECT @DeletedRequestLogs AS DeletedRequestLogs, @DeletedErrorEvents AS DeletedErrorEvents;
END;
GO

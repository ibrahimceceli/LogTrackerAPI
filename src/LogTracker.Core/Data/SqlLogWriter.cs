using System.Data;
using Dapper;
using LogTracker.Core.Pipeline;

namespace LogTracker.Core.Data;

/// <summary>
/// Writes batches through table-valued parameters: one stored procedure call inserts the whole batch.
/// </summary>
internal sealed class SqlLogWriter(SqlConnectionFactory connectionFactory) : ILogWriter
{
    // Deleting a month of rows on a busy database can take a while.
    private const int PurgeTimeoutSeconds = 300;

    public async Task WriteRequestsAsync(IReadOnlyList<RequestLogEntry> entries, CancellationToken cancellationToken)
    {
        // Column order must match dbo.RequestLogTableType.
        using var table = new DataTable();
        table.Columns.Add("OccurredAtUtc", typeof(DateTime));
        table.Columns.Add("Method", typeof(string));
        table.Columns.Add("Endpoint", typeof(string));
        table.Columns.Add("Path", typeof(string));
        table.Columns.Add("StatusCode", typeof(short));
        table.Columns.Add("DurationMs", typeof(int));
        table.Columns.Add("TraceId", typeof(string));

        foreach (var entry in entries)
        {
            table.Rows.Add(entry.OccurredAtUtc, entry.Method, entry.Endpoint, entry.Path,
                (short)entry.StatusCode, entry.DurationMs, (object?)entry.TraceId ?? DBNull.Value);
        }

        await ExecuteProcedureAsync("dbo.InsertRequestLogs", table.AsTableValuedParameter("dbo.RequestLogTableType"), cancellationToken);
    }

    public async Task WriteErrorsAsync(IReadOnlyList<ErrorEventEntry> entries, CancellationToken cancellationToken)
    {
        // Column order must match dbo.ErrorEventTableType.
        using var table = new DataTable();
        table.Columns.Add("OccurredAtUtc", typeof(DateTime));
        table.Columns.Add("ErrorType", typeof(string));
        table.Columns.Add("Message", typeof(string));
        table.Columns.Add("Source", typeof(string));
        table.Columns.Add("StackTrace", typeof(string));
        table.Columns.Add("Method", typeof(string));
        table.Columns.Add("Path", typeof(string));
        table.Columns.Add("Endpoint", typeof(string));
        table.Columns.Add("StatusCode", typeof(short));
        table.Columns.Add("TraceId", typeof(string));

        foreach (var entry in entries)
        {
            table.Rows.Add(entry.OccurredAtUtc, entry.ErrorType, entry.Message,
                (object?)entry.Source ?? DBNull.Value, (object?)entry.StackTrace ?? DBNull.Value,
                entry.Method, entry.Path, entry.Endpoint, (short)entry.StatusCode,
                (object?)entry.TraceId ?? DBNull.Value);
        }

        await ExecuteProcedureAsync("dbo.InsertErrorEvents", table.AsTableValuedParameter("dbo.ErrorEventTableType"), cancellationToken);
    }

    public async Task<PurgeResult> PurgeAsync(int retentionDays, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        return await connection.QuerySingleAsync<PurgeResult>(new CommandDefinition(
            "dbo.PurgeOldLogs",
            new { RetentionDays = retentionDays },
            commandType: CommandType.StoredProcedure,
            commandTimeout: PurgeTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private async Task ExecuteProcedureAsync(string procedure, SqlMapper.ICustomQueryParameter entries, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            procedure,
            new { Entries = entries },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }
}

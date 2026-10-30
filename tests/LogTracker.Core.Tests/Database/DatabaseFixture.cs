using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LogTracker.Core.Data;
using LogTracker.Core.Tests.Fakes;
using Microsoft.Data.SqlClient;

namespace LogTracker.Core.Tests.Database;

/// <summary>A fact that only runs when <c>LOGTRACKER_TEST_CONNECTION</c> points at a SQL Server.</summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!TestDatabase.IsConfigured)
        {
            Skip = $"Set {TestDatabase.EnvironmentVariable} to a SQL Server connection string to run database tests.";
        }
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}

/// <summary>One fresh database with the schema scripts applied, shared by all database tests and dropped afterwards.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private TestDatabase? _database;

    public string ConnectionString => _database?.ConnectionString
        ?? throw new InvalidOperationException($"{TestDatabase.EnvironmentVariable} is not set.");

    internal SqlConnectionFactory ConnectionFactory =>
        new(TestOptions.Create(options => options.ConnectionString = ConnectionString));

    public async ValueTask InitializeAsync()
    {
        if (!TestDatabase.IsConfigured)
        {
            return;
        }

        _database = await TestDatabase.CreateAsync();
        await TestDatabase.RunSchemaScriptsAsync(_database.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}

/// <summary>Creates a uniquely named database on the test server and drops it when disposed.</summary>
public sealed partial class TestDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "LOGTRACKER_TEST_CONNECTION";

    private readonly string _serverConnectionString;
    private readonly string _name;

    private TestDatabase(string serverConnectionString, string name)
    {
        _serverConnectionString = serverConnectionString;
        _name = name;
        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ServerConnectionString);

    public string ConnectionString { get; }

    private static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

    private static string ScriptDirectory => Path.Combine(AppContext.BaseDirectory, "database");

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase(ServerConnectionString!, $"LogTrackerTests_{Guid.NewGuid():N}");
        await database.ExecuteOnServerAsync($"CREATE DATABASE [{database._name}];");
        return database;
    }

    /// <summary>All numbered scripts except the optional 9x demo data, in the same order sqlcmd would run them.</summary>
    public static async Task RunSchemaScriptsAsync(string connectionString)
    {
        var scripts = Directory.GetFiles(ScriptDirectory, "*.sql")
            .Where(path => !Path.GetFileName(path).StartsWith('9'))
            .Order(StringComparer.Ordinal);

        foreach (var script in scripts)
        {
            await RunScriptAsync(connectionString, script);
        }
    }

    public static async Task RunScriptAsync(string connectionString, string scriptPath)
    {
        if (!Path.IsPathRooted(scriptPath))
        {
            scriptPath = Path.Combine(ScriptDirectory, scriptPath);
        }

        var sql = await File.ReadAllTextAsync(scriptPath, TestContext.Current.CancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // "GO" is a sqlcmd batch separator, not T-SQL, so the batches are sent one by one.
        foreach (var batch in GoSeparator().Split(sql).Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteOnServerAsync($"""
            ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{_name}];
            """);
    }

    // Not tied to the test's cancellation token: the database must be dropped even when a run is cancelled.
    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new SqlConnection(_serverConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}

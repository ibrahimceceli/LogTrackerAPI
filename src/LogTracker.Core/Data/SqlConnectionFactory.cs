using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Data;

/// <summary>Creates connections to the log database. Connection pooling makes this cheap.</summary>
internal sealed class SqlConnectionFactory(IOptions<LogTrackerOptions> options)
{
    public SqlConnection Create() => new(options.Value.ConnectionString);
}

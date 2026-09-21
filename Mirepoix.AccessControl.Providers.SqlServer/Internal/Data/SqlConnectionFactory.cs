using Microsoft.Data.SqlClient;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

/// <summary>
/// Creates <see cref="SqlConnection"/> instances from a fixed connection string.
/// Does not pool beyond what <see cref="SqlConnection"/> / ADO already do; callers dispose connections.
/// </summary>
internal sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a factory. <paramref name="connectionString"/> must be non-empty.
    /// </summary>
    public SqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <summary>Returns a new closed <see cref="SqlConnection"/>.</summary>
    public SqlConnection Create() => new(_connectionString);
}

using Microsoft.Data.SqlClient;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

internal sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public SqlConnection Create() => new(_connectionString);
}

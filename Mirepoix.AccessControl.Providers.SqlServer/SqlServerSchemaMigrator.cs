using Microsoft.Data.SqlClient;
using Mirepoix.AccessControl.Providers.Schema;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

public sealed class SqlServerSchemaMigrator
{
    private readonly SqlConnectionFactory _connections;

    public SqlServerSchemaMigrator(SqlServerProviderOptions options)
        : this(new SqlConnectionFactory(options.ConnectionString))
    {
    }

    internal SqlServerSchemaMigrator(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var script = AccessControlSchemaScripts.LoadSqlServerInit();
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var batch in AccessControlSchemaScripts.SplitBatches(script, AccessControlSchemaDialect.SqlServer))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

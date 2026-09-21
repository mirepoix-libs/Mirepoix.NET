using Mirepoix.AccessControl.Providers.Schema;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Applies the embedded SqlServer <c>ac_*</c> init script (GO-batched) against the configured connection.
/// Call once at startup for package-driven schema setup. Scripts are expected to be idempotent.
/// </summary>
public sealed class SqlServerSchemaMigrator
{
    private readonly SqlConnectionFactory _connections;

    /// <summary>
    /// Creates a migrator from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Connection string source.</param>
    public SqlServerSchemaMigrator(SqlServerProviderOptions options)
        : this(new SqlConnectionFactory(options.ConnectionString))
    {
    }

    /// <summary>
    /// Creates a migrator with an injected connection factory (package/tests).
    /// </summary>
    internal SqlServerSchemaMigrator(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Opens a connection and executes each SqlServer script batch sequentially.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for open/execute.</param>
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

using Mirepoix.AccessControl.Providers.Schema;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Applies ordered embedded SqlServer <c>ac_*</c> scripts against the configured connection.
/// Selects subject scripts from the configured mapping layout, then executes each <c>GO</c>-delimited batch.
/// Call once at startup for package-driven schema setup. Scripts are expected to be idempotent.
/// </summary>
public sealed class SqlServerSchemaMigrator
{
    private readonly SqlConnectionFactory _connections;
    private readonly SubjectStorageLayout _layout;

    /// <summary>
    /// Creates a migrator from <paramref name="options"/> and resolves subject-table ownership from its mappings.
    /// </summary>
    /// <param name="options">Connection string and subject mapping source.</param>
    public SqlServerSchemaMigrator(SqlServerProviderOptions options)
        : this(
            new SqlConnectionFactory(options.ConnectionString),
            SubjectStorageLayoutResolver.Resolve(options.SubjectMapping))
    {
    }

    /// <summary>
    /// Creates a migrator with an injected connection factory and storage layout (package/tests).
    /// </summary>
    internal SqlServerSchemaMigrator(
        SqlConnectionFactory connections,
        SubjectStorageLayout layout = SubjectStorageLayout.Native)
    {
        _connections = connections;
        _layout = layout;
    }

    /// <summary>
    /// Opens a connection and executes each selected SqlServer script and batch sequentially.
    /// Native storage includes subject-role and subject-header scripts; mapped library-role storage includes only
    /// subject roles; mapped app-owned role storage includes neither.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for open/execute.</param>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var resourceName in ResourceNames(_layout))
        {
            var script = AccessControlSchemaScripts.Load(resourceName);
            foreach (var batch in AccessControlSchemaScripts.SplitBatches(
                         script,
                         AccessControlSchemaDialect.SqlServer))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Selects core and management scripts for every layout, the subject-role script when roles are library-owned,
    /// and the subject-header script only for native storage.
    /// </summary>
    internal static IReadOnlyList<string> ResourceNames(SubjectStorageLayout layout) =>
        AccessControlSchemaDialectMap.ResourceNames(AccessControlSchemaDialect.SqlServer, layout);
}

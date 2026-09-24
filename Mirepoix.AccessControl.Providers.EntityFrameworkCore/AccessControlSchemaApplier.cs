using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Applies the embedded dialect schema scripts for package-driven mode against <see cref="AccessControlDbContext"/>.
/// Resolves dialect from the EF database provider name via <see cref="AccessControlSchemaDialectMap"/>.
/// Applies core and management scripts for every layout, subject-role DDL when library-owned, native subject DDL
/// only for native storage, and the legacy <c>ac_resource_attribute</c> drop script last on every layout.
/// Only SqlServer and PostgreSQL provider names are supported; other providers need app-owned migrations.
/// Registered only by package-driven DI helpers.
/// </summary>
public sealed class AccessControlSchemaApplier
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Creates an applier that resolves <see cref="AccessControlDbContext"/> from a scope.
    /// </summary>
    /// <param name="scopeFactory">DI scope factory.</param>
    public AccessControlSchemaApplier(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Opens the context connection if needed and executes each ordered script batch for the resolved dialect and
    /// configured subject-storage layout, including the idempotent legacy resource-attribute drop script.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for open/execute.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when the EF provider name is not SqlServer or PostgreSQL.
    /// </exception>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<EntityFrameworkProviderOptions>();

        var dialect = AccessControlSchemaDialectMap.Resolve(context.Database.ProviderName);
        var layout = SubjectStorageLayoutResolver.Resolve(options.SubjectMapping);

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var resourceName in ResourceNames(dialect, layout))
        {
            var script = AccessControlSchemaScripts.Load(resourceName);
            foreach (var batch in AccessControlSchemaScripts.SplitBatches(script, dialect))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static IReadOnlyList<string> ResourceNames(
        AccessControlSchemaDialect dialect,
        SubjectStorageLayout layout) =>
        AccessControlSchemaDialectMap.ResourceNames(dialect, layout);
}

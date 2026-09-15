using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers;

public sealed class AccessControlSchemaApplier
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AccessControlSchemaApplier(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();

        var dialect = AccessControlSchemaDialectMap.Resolve(context.Database.ProviderName);
        var script = AccessControlSchemaScripts.Load(AccessControlSchemaDialectMap.ResourceName(dialect));

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var batch in AccessControlSchemaScripts.SplitBatches(script, dialect))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

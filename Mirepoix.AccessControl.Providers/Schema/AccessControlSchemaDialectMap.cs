namespace Mirepoix.AccessControl.Providers.Schema;

public enum AccessControlSchemaDialect
{
    SqlServer,
    PostgreSql,
}

public static class AccessControlSchemaDialectMap
{
    public const string SqlServerProviderName = "Microsoft.EntityFrameworkCore.SqlServer";
    public const string PostgreSqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public const string SqlServerInitResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.001_init.sql";

    public const string PostgreSqlInitResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.001_init.sql";

    public static AccessControlSchemaDialect Resolve(string? providerName)
    {
        if (string.Equals(providerName, SqlServerProviderName, StringComparison.Ordinal))
            return AccessControlSchemaDialect.SqlServer;

        if (string.Equals(providerName, PostgreSqlProviderName, StringComparison.Ordinal))
            return AccessControlSchemaDialect.PostgreSql;

        throw new NotSupportedException(
            $"AccessControl package-driven schema apply does not support provider '{providerName}'. " +
            "Use SqlServer or PostgreSQL, or switch to app-owned migrations (AddAccessControlProviders<TContext>).");
    }

    public static string ResourceName(AccessControlSchemaDialect dialect) =>
        dialect switch
        {
            AccessControlSchemaDialect.SqlServer => SqlServerInitResource,
            AccessControlSchemaDialect.PostgreSql => PostgreSqlInitResource,
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };
}

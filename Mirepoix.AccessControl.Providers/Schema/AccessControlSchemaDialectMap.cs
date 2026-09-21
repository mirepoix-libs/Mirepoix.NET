namespace Mirepoix.AccessControl.Providers.Schema;

/// <summary>
/// Selects the SQL dialect for embedded init scripts and batch splitting.
/// </summary>
public enum AccessControlSchemaDialect
{
    /// <summary>Uses SQL Server scripts; batches split on <c>GO</c>.</summary>
    SqlServer,

    /// <summary>Uses PostgreSQL scripts; treated as a single batch (no <c>GO</c> splitting).</summary>
    PostgreSql,
}

/// <summary>
/// Maps known database provider names to <see cref="AccessControlSchemaDialect"/> and embedded resource names.
/// Provider name constants match common EF Core provider identifiers so package-driven schema apply can
/// pick a script from provider metadata without hard-coding adapter packages here.
/// </summary>
public static class AccessControlSchemaDialectMap
{
    /// <summary>Names the EF Core SqlServer provider string (ordinal match in <see cref="Resolve"/>).</summary>
    public const string SqlServerProviderName = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>Names the EF Core Npgsql provider string (ordinal match in <see cref="Resolve"/>).</summary>
    public const string PostgreSqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Names the manifest resource for the SqlServer init script embedded in this assembly.</summary>
    public const string SqlServerInitResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.001_init.sql";

    /// <summary>Names the manifest resource for the PostgreSQL init script embedded in this assembly.</summary>
    public const string PostgreSqlInitResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.001_init.sql";

    /// <summary>
    /// Resolves <paramref name="providerName"/> to a dialect via ordinal equality against
    /// <see cref="SqlServerProviderName"/> / <see cref="PostgreSqlProviderName"/>.
    /// </summary>
    /// <param name="providerName">Database provider name (e.g. from EF Core). May be null.</param>
    /// <returns>Matching dialect.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the name is not one of the two known providers.
    /// </exception>
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

    /// <summary>
    /// Returns the embedded resource name for <paramref name="dialect"/>'s init script.
    /// </summary>
    /// <param name="dialect">Target dialect.</param>
    /// <returns>Manifest resource logical name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined dialect value.</exception>
    public static string ResourceName(AccessControlSchemaDialect dialect) =>
        dialect switch
        {
            AccessControlSchemaDialect.SqlServer => SqlServerInitResource,
            AccessControlSchemaDialect.PostgreSql => PostgreSqlInitResource,
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };
}

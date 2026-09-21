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

    /// <summary>Names the manifest resource for the SqlServer core schema script.</summary>
    public const string SqlServerCoreResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.001_init.sql";

    /// <summary>Names the manifest resource for the SqlServer subject-role schema script.</summary>
    public const string SqlServerSubjectRolesResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.001b_subject_roles.sql";

    /// <summary>Names the manifest resource for the SqlServer management schema script.</summary>
    public const string SqlServerManagementResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.002_management.sql";

    /// <summary>Names the manifest resource for the SqlServer native-subject schema script.</summary>
    public const string SqlServerSubjectsResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.SqlServer.002b_subjects.sql";

    /// <summary>Names the manifest resource for the PostgreSQL core schema script.</summary>
    public const string PostgreSqlCoreResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.001_init.sql";

    /// <summary>Names the manifest resource for the PostgreSQL subject-role schema script.</summary>
    public const string PostgreSqlSubjectRolesResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.001b_subject_roles.sql";

    /// <summary>Names the manifest resource for the PostgreSQL management schema script.</summary>
    public const string PostgreSqlManagementResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.002_management.sql";

    /// <summary>Names the manifest resource for the PostgreSQL native-subject schema script.</summary>
    public const string PostgreSqlSubjectsResource =
        "Mirepoix.AccessControl.Providers.Schema.Scripts.PostgreSql.002b_subjects.sql";

    /// <summary>Names the SqlServer core resource for callers using the original single-script API.</summary>
    public const string SqlServerInitResource = SqlServerCoreResource;

    /// <summary>Names the PostgreSQL core resource for callers using the original single-script API.</summary>
    public const string PostgreSqlInitResource = PostgreSqlCoreResource;

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
            AccessControlSchemaDialect.SqlServer => SqlServerCoreResource,
            AccessControlSchemaDialect.PostgreSql => PostgreSqlCoreResource,
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };

    /// <summary>
    /// Lists core, subject-role, management, then native-subject resource names for a dialect in schema-application
    /// order. Callers omit subject resources according to the resolved storage layout.
    /// </summary>
    /// <param name="dialect">Target dialect.</param>
    /// <returns>Ordered embedded resource names.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined dialect value.</exception>
    public static IReadOnlyList<string> ResourceNames(AccessControlSchemaDialect dialect) =>
        dialect switch
        {
            AccessControlSchemaDialect.SqlServer =>
                [
                    SqlServerCoreResource,
                    SqlServerSubjectRolesResource,
                    SqlServerManagementResource,
                    SqlServerSubjectsResource,
                ],
            AccessControlSchemaDialect.PostgreSql =>
                [
                    PostgreSqlCoreResource,
                    PostgreSqlSubjectRolesResource,
                    PostgreSqlManagementResource,
                    PostgreSqlSubjectsResource,
                ],
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };

    /// <summary>
    /// Lists schema resources for <paramref name="dialect"/>, filtered to the subject storage owned by
    /// <paramref name="layout"/>.
    /// </summary>
    /// <param name="dialect">Target dialect.</param>
    /// <param name="layout">Subject and role storage owned by the library.</param>
    /// <returns>Ordered embedded resource names required by the layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined layout value.</exception>
    public static IReadOnlyList<string> ResourceNames(
        AccessControlSchemaDialect dialect,
        SubjectStorageLayout layout)
    {
        var resources = ResourceNames(dialect);
        return layout switch
        {
            SubjectStorageLayout.Native => resources,
            SubjectStorageLayout.MappedLibraryRoles => resources.Take(3).ToArray(),
            SubjectStorageLayout.MappedAppOwnedRoles => [resources[0], resources[2]],
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
        };
    }
}


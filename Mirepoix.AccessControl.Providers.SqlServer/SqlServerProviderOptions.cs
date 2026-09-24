namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds connection and mapping settings for SqlServer ADO read/hydrate adapters.
/// Connection string is required on the first slice that registers shared services.
/// </summary>
public sealed class SqlServerProviderOptions
    : AccessControlProviderOptions<SqlServerProviderOptions>
{
    /// <summary>
    /// Holds the SQL Server connection string used by policy/subject/resource readers and the schema migrator.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Controls how long a loaded policy set stays cached. <c>null</c> caches until process recycle
    /// (no periodic re-read).
    /// </summary>
    public TimeSpan? PolicyCacheTtl { get; set; }
}

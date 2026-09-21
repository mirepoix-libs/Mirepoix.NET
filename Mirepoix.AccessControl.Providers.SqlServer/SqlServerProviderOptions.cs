namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds connection and mapping settings for SqlServer ADO read/hydrate adapters.
/// Connection string is required on the first slice that registers shared services.
/// </summary>
public sealed class SqlServerProviderOptions
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

    /// <summary>
    /// Holds optional maps from app entity types to <see cref="Subject"/>.
    /// Empty maps mean hydrate from built-in <c>ac_subject*</c> tables.
    /// Mapped mode does not merge with <c>ac_subject*</c>.
    /// </summary>
    public SubjectMappingOptions SubjectMapping { get; } = new();

    /// <summary>
    /// Adds a subject entity map and returns this instance for chaining.
    /// </summary>
    /// <typeparam name="T">CLR entity type.</typeparam>
    /// <param name="configure">Fluent map configuration (table hints required for ADO when maps are used).</param>
    /// <returns>This options instance.</returns>
    public SqlServerProviderOptions MapSubject<T>(Action<SubjectEntityMapBuilder<T>> configure)
        where T : class
    {
        SubjectMapping.MapEntity(configure);
        return this;
    }
}

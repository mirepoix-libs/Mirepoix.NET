namespace Mirepoix.AccessControl.Providers;

public sealed class SqlServerProviderOptions
{
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// How long a loaded policy set stays cached. <c>null</c> caches until process recycle
    /// (no periodic re-read).
    /// </summary>
    public TimeSpan? PolicyCacheTtl { get; set; }
}

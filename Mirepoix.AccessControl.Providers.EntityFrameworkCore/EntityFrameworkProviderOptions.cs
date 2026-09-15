using Microsoft.EntityFrameworkCore;

namespace Mirepoix.AccessControl.Providers;

public sealed class EntityFrameworkProviderOptions
{
    public TimeSpan? PolicyCacheTtl { get; set; }

    /// <summary>
    /// Package-driven only. Required on <see cref="AccessControlEntityFrameworkServiceCollectionExtensions.AddAccessControlProviders"/>.
    /// Configure the EF provider here (e.g. <c>db =&gt; db.UseSqlServer(cs)</c>).
    /// Ignored on <c>AddAccessControlProviders&lt;TContext&gt;</c>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureDb { get; set; }

    /// <summary>
    /// Set by DI registration to the <see cref="DbContext"/> type seams should resolve.
    /// </summary>
    public Type? ContextType { get; set; }
}

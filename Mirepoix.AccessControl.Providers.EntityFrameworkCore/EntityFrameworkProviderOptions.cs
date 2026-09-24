using Microsoft.EntityFrameworkCore;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds options for EF Core read/hydrate adapters.
/// Mode is chosen by which DI overload registers the seams (package-driven vs app-owned context),
/// not by a flag on this type.
/// </summary>
public sealed class EntityFrameworkProviderOptions
    : AccessControlProviderOptions<EntityFrameworkProviderOptions>
{
    /// <summary>
    /// Controls how long a loaded policy set stays cached. <c>null</c> caches until process recycle.
    /// </summary>
    public TimeSpan? PolicyCacheTtl { get; set; }

    /// <summary>
    /// Configures the EF database provider for package-driven registration.
    /// Required on non-generic <c>AddAccessControlProviders</c> / policy slice registration
    /// (e.g. <c>db =&gt; db.UseSqlServer(cs)</c>).
    /// Ignored on generic <c>AddAccessControlProviders&lt;TContext&gt;</c> (app owns <see cref="DbContext"/> registration).
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureDb { get; set; }

    /// <summary>
    /// Holds the <see cref="DbContext"/> type seams should resolve
    /// (<see cref="AccessControlDbContext"/> or the app's <c>TContext</c>). Set by DI registration.
    /// </summary>
    public Type? ContextType { get; set; }
}

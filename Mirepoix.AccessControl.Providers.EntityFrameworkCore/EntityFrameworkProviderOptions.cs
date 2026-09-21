using Microsoft.EntityFrameworkCore;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds options for EF Core read/hydrate adapters.
/// Mode is chosen by which DI overload registers the seams (package-driven vs app-owned context),
/// not by a flag on this type.
/// </summary>
public sealed class EntityFrameworkProviderOptions
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

    /// <summary>
    /// Holds optional maps from app entity types to <see cref="Subject"/>.
    /// Empty maps mean hydrate from built-in <c>ac_subject*</c> entities.
    /// Maps without explicit role members union roles from library-owned <c>ac_subject_role</c>;
    /// maps with role members own role storage. Storage table hints are optional (EF model preferred).
    /// </summary>
    public SubjectMappingOptions SubjectMapping { get; } = new();

    /// <summary>
    /// Adds a subject entity map and returns this instance for chaining.
    /// </summary>
    /// <typeparam name="T">CLR entity type present on the resolved <see cref="DbContext"/>.</typeparam>
    /// <param name="configure">Fluent map configuration.</param>
    /// <returns>This options instance.</returns>
    public EntityFrameworkProviderOptions MapSubject<T>(Action<SubjectEntityMapBuilder<T>> configure)
        where T : class
    {
        SubjectMapping.MapEntity(configure);
        return this;
    }
}

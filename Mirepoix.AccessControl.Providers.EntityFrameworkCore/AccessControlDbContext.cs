using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Exposes the shared <c>ac_*</c> model as a package-owned <see cref="DbContext"/>.
/// Registered by package-driven DI helpers. Applies <see cref="AccessControlModelBuilderExtensions.ApplyAccessControl"/>
/// in <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AccessControlDbContext : DbContext
{
    /// <summary>
    /// Creates the context with EF options (provider configured by the app via <see cref="EntityFrameworkProviderOptions.ConfigureDb"/>).
    /// </summary>
    /// <param name="options">EF options for this context type.</param>
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options)
        : base(options)
    {
    }

    /// <summary>Exposes policy set rows (singleton id 1 in normal use).</summary>
    public DbSet<PolicySetEntity> PolicySets => Set<PolicySetEntity>();

    /// <summary>Exposes subject identity rows.</summary>
    public DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();

    /// <summary>Exposes subject role rows.</summary>
    public DbSet<SubjectRoleEntity> SubjectRoles => Set<SubjectRoleEntity>();

    /// <summary>Exposes subject attribute rows.</summary>
    public DbSet<SubjectAttributeEntity> SubjectAttributes => Set<SubjectAttributeEntity>();

    /// <summary>Exposes resource attribute rows.</summary>
    public DbSet<ResourceAttributeEntity> ResourceAttributes => Set<ResourceAttributeEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Exposes the shared <c>ac_*</c> model as a package-owned <see cref="DbContext"/>.
/// Registered by package-driven DI helpers. Applies <see cref="AccessControlModelBuilderExtensions.ApplyAccessControl"/>
/// in <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AccessControlDbContext : DbContext
{
    private readonly SubjectStorageLayout _layout;
    internal SubjectStorageLayout Layout => _layout;

    /// <summary>
    /// Creates the context with EF options and selects subject-table ownership from the registered provider options.
    /// Direct construction without provider options uses native subject storage.
    /// </summary>
    /// <param name="options">EF options for this context type.</param>
    /// <param name="providerOptions">Optional provider subject mapping used to select the model layout.</param>
    public AccessControlDbContext(
        DbContextOptions<AccessControlDbContext> options,
        EntityFrameworkProviderOptions? providerOptions = null)
        : base(options)
    {
        _layout = providerOptions is null
            ? SubjectStorageLayout.Native
            : SubjectStorageLayoutResolver.Resolve(providerOptions.SubjectMapping);
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
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, AccessControlModelCacheKeyFactory>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl(_layout);
    }
}

internal sealed class AccessControlModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is AccessControlDbContext accessControlContext
            ? (context.GetType(), accessControlContext.Layout, designTime)
            : (context.GetType(), designTime);

    public object Create(DbContext context) => Create(context, designTime: false);
}

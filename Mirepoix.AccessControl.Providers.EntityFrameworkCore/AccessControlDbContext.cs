using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

public sealed class AccessControlDbContext : DbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<PolicySetEntity> PolicySets => Set<PolicySetEntity>();
    public DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();
    public DbSet<SubjectRoleEntity> SubjectRoles => Set<SubjectRoleEntity>();
    public DbSet<SubjectAttributeEntity> SubjectAttributes => Set<SubjectAttributeEntity>();
    public DbSet<ResourceAttributeEntity> ResourceAttributes => Set<ResourceAttributeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl();
    }
}

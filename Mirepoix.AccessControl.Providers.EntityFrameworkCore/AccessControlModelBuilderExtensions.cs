using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Maps shared <c>ac_*</c> entity types onto <see cref="AccessControlSchema"/> table/column names.
/// Call from an app <see cref="DbContext.OnModelCreating"/> in app-owned mode, or rely on
/// <see cref="AccessControlDbContext"/> which calls this automatically.
/// </summary>
public static class AccessControlModelBuilderExtensions
{
    /// <summary>
    /// Configures the always-owned policy, role-catalog, and separation-of-duties entities,
    /// then adds only the subject entities owned by <paramref name="layout"/>.
    /// Subject role rows remain standalone and never have a foreign key to subject headers.
    /// </summary>
    /// <param name="modelBuilder">EF model builder.</param>
    /// <param name="layout">Subject and role storage owned by the library.</param>
    /// <returns><paramref name="modelBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="layout"/> is undefined.</exception>
    public static ModelBuilder ApplyAccessControl(
        this ModelBuilder modelBuilder,
        SubjectStorageLayout layout = SubjectStorageLayout.Native)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PolicySetEntity>(e =>
        {
            e.ToTable(AccessControlSchema.PolicySetTable);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName(AccessControlSchema.ColId);
            e.Property(x => x.Version).HasColumnName(AccessControlSchema.ColVersion).HasMaxLength(128).IsRequired();
            e.Property(x => x.PayloadJson).HasColumnName(AccessControlSchema.ColPayloadJson).IsRequired();
            e.Property(x => x.UpdatedUtc).HasColumnName(AccessControlSchema.ColUpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<RoleEntity>(e =>
        {
            e.ToTable(AccessControlSchema.RoleTable);
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleId).HasColumnName(AccessControlSchema.ColRoleId).HasMaxLength(256);
            e.Property(x => x.Description).HasColumnName(AccessControlSchema.ColDescription);
        });

        modelBuilder.Entity<SodConstraintEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SodConstraintTable);
            e.HasKey(x => x.ConstraintId);
            e.Property(x => x.ConstraintId).HasColumnName(AccessControlSchema.ColConstraintId).HasMaxLength(256);
        });

        modelBuilder.Entity<SodConstraintRoleEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SodConstraintRoleTable);
            e.HasKey(x => new { x.ConstraintId, x.RoleId });
            e.Property(x => x.ConstraintId).HasColumnName(AccessControlSchema.ColConstraintId).HasMaxLength(256);
            e.Property(x => x.RoleId).HasColumnName(AccessControlSchema.ColRoleId).HasMaxLength(256);
            e.HasOne(x => x.Constraint)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.ConstraintId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        switch (layout)
        {
            case SubjectStorageLayout.Native:
                ConfigureNativeSubjects(modelBuilder);
                ConfigureSubjectRoles(modelBuilder);
                break;
            case SubjectStorageLayout.MappedLibraryRoles:
                modelBuilder.Ignore<SubjectEntity>();
                modelBuilder.Ignore<SubjectAttributeEntity>();
                ConfigureSubjectRoles(modelBuilder);
                break;
            case SubjectStorageLayout.MappedAppOwnedRoles:
                modelBuilder.Ignore<SubjectEntity>();
                modelBuilder.Ignore<SubjectAttributeEntity>();
                modelBuilder.Ignore<SubjectRoleEntity>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
        }

        return modelBuilder;
    }

    private static void ConfigureNativeSubjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubjectEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SubjectTable);
            e.HasKey(x => x.SubjectId);
            e.Property(x => x.SubjectId).HasColumnName(AccessControlSchema.ColSubjectId).HasMaxLength(256);
        });

        modelBuilder.Entity<SubjectAttributeEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SubjectAttributeTable);
            e.HasKey(x => new { x.SubjectId, x.Name });
            e.Property(x => x.SubjectId).HasColumnName(AccessControlSchema.ColSubjectId).HasMaxLength(256);
            e.Property(x => x.Name).HasColumnName(AccessControlSchema.ColName).HasMaxLength(256);
            e.Property(x => x.ValueJson).HasColumnName(AccessControlSchema.ColValueJson);
            e.HasOne(x => x.Subject)
                .WithMany(x => x.Attributes)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSubjectRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubjectRoleEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SubjectRoleTable);
            e.HasKey(x => new { x.SubjectId, x.Role });
            e.Property(x => x.SubjectId).HasColumnName(AccessControlSchema.ColSubjectId).HasMaxLength(256);
            e.Property(x => x.Role).HasColumnName(AccessControlSchema.ColRole).HasMaxLength(256);
        });
    }
}

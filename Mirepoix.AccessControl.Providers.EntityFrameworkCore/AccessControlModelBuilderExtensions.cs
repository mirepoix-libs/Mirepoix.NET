using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers;

public static class AccessControlModelBuilderExtensions
{
    public static ModelBuilder ApplyAccessControl(this ModelBuilder modelBuilder)
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

        modelBuilder.Entity<SubjectEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SubjectTable);
            e.HasKey(x => x.SubjectId);
            e.Property(x => x.SubjectId).HasColumnName(AccessControlSchema.ColSubjectId).HasMaxLength(256);
        });

        modelBuilder.Entity<SubjectRoleEntity>(e =>
        {
            e.ToTable(AccessControlSchema.SubjectRoleTable);
            e.HasKey(x => new { x.SubjectId, x.Role });
            e.Property(x => x.SubjectId).HasColumnName(AccessControlSchema.ColSubjectId).HasMaxLength(256);
            e.Property(x => x.Role).HasColumnName(AccessControlSchema.ColRole).HasMaxLength(256);
            e.HasOne(x => x.Subject)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<ResourceAttributeEntity>(e =>
        {
            e.ToTable(AccessControlSchema.ResourceAttributeTable);
            e.HasKey(x => new { x.ResourceType, x.ResourceId, x.Name });
            e.Property(x => x.ResourceType).HasColumnName(AccessControlSchema.ColResourceType).HasMaxLength(256);
            e.Property(x => x.ResourceId).HasColumnName(AccessControlSchema.ColResourceId).HasMaxLength(256);
            e.Property(x => x.Name).HasColumnName(AccessControlSchema.ColName).HasMaxLength(256);
            e.Property(x => x.ValueJson).HasColumnName(AccessControlSchema.ColValueJson);
        });

        return modelBuilder;
    }
}

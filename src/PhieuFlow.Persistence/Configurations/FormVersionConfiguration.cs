using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class FormVersionConfiguration : IEntityTypeConfiguration<FormVersion>
{
    public void Configure(EntityTypeBuilder<FormVersion> builder)
    {
        builder.ToTable("FormVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.VersionNumber).IsRequired().HasDefaultValue(1);
        builder.Property(v => v.Title).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Description).HasMaxLength(2000);
        builder.Property(v => v.Revision).IsRequired().HasDefaultValue(1);
        builder.Property(v => v.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.LastModifiedAt).IsRequired();
        builder.Property(v => v.LastModifiedBy).HasMaxLength(256);

        builder.HasIndex(v => new { v.FormId, v.VersionNumber }).IsUnique();

        builder.HasMany(v => v.Pages)
            .WithOne(p => p.FormVersion)
            .HasForeignKey(p => p.FormVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

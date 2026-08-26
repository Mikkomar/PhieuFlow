using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Hub.Data.Configurations;

public class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("Forms");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Title).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.Revision).IsRequired().HasDefaultValue(1);
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.LastModifiedAt).IsRequired();
        builder.Property(f => f.LastModifiedBy).HasMaxLength(256);

        // No explicit ordering column yet; page order is not guaranteed on reload.
        builder.HasMany(f => f.Pages)
            .WithOne(p => p.Form)
            .HasForeignKey(p => p.FormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

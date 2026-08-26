using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class FormPageConfiguration : IEntityTypeConfiguration<FormPage>
{
    public void Configure(EntityTypeBuilder<FormPage> builder)
    {
        builder.ToTable("FormPages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Title).HasMaxLength(200);

        // No explicit ordering column yet; question order is not guaranteed on reload.
        builder.HasMany(p => p.Questions)
            .WithOne(q => q.FormPage)
            .HasForeignKey(q => q.FormPageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

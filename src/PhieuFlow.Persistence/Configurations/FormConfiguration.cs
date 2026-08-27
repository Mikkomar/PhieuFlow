using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("Forms");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasMany(f => f.Versions)
            .WithOne(v => v.Form)
            .HasForeignKey(v => v.FormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

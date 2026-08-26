using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class ChoiceQuestionConfiguration : IEntityTypeConfiguration<ChoiceQuestion>
{
    public void Configure(EntityTypeBuilder<ChoiceQuestion> builder)
    {
        // No explicit ordering column yet; option order is not guaranteed on reload.
        builder.HasMany(c => c.Options)
            .WithOne()
            .HasForeignKey("ChoiceQuestionId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

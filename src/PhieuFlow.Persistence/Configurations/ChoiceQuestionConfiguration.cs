using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class ChoiceQuestionConfiguration : IEntityTypeConfiguration<ChoiceQuestion>
{
    public void Configure(EntityTypeBuilder<ChoiceQuestion> builder)
    {
        // Options carry an explicit Order column; FormRepository sorts by it on reload.
        builder.HasMany(c => c.Options)
            .WithOne()
            .HasForeignKey("ChoiceQuestionId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

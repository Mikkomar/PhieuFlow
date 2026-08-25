using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Hub.Data.Configurations;

public class CheckboxQuestionConfiguration : IEntityTypeConfiguration<CheckboxQuestion>
{
    public void Configure(EntityTypeBuilder<CheckboxQuestion> builder)
    {
        builder.Property(c => c.Label).IsRequired().HasMaxLength(200);
    }
}

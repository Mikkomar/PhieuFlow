using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class OptionSubmissionAnswerConfiguration : IEntityTypeConfiguration<OptionSubmissionAnswer>
{
    public void Configure(EntityTypeBuilder<OptionSubmissionAnswer> builder)
    {
        builder.Property(a => a.OptionId).IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class ValueSubmissionAnswerConfiguration : IEntityTypeConfiguration<ValueSubmissionAnswer>
{
    public void Configure(EntityTypeBuilder<ValueSubmissionAnswer> builder)
    {
        builder.Property(a => a.Value).HasMaxLength(4000);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Hub.Data.Configurations;

public class NumberQuestionConfiguration : IEntityTypeConfiguration<NumberQuestion>
{
    public void Configure(EntityTypeBuilder<NumberQuestion> builder)
    {
        builder.Property(q => q.Min).HasPrecision(18, 4);
        builder.Property(q => q.Max).HasPrecision(18, 4);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        // MessageId is the dedup key, so it is the primary key. A concurrent second delivery
        // loses the insert race with a unique-key violation.
        builder.HasKey(m => m.MessageId);
        builder.Property(m => m.MessageId).ValueGeneratedNever();

        builder.Property(m => m.ProcessedAt).IsRequired();
    }
}

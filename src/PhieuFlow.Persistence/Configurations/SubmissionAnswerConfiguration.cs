using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class SubmissionAnswerConfiguration : IEntityTypeConfiguration<SubmissionAnswer>
{
    public void Configure(EntityTypeBuilder<SubmissionAnswer> builder)
    {
        builder.ToTable("SubmissionAnswers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.QuestionId).IsRequired();
        builder.Property(a => a.QuestionText).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Order).IsRequired();

        builder.HasIndex(a => a.FormSubmissionId);

        builder.HasDiscriminator<string>("AnswerType")
            .HasValue<ValueSubmissionAnswer>("Value")
            .HasValue<BooleanSubmissionAnswer>("Boolean")
            .HasValue<OptionSubmissionAnswer>("Option");
    }
}

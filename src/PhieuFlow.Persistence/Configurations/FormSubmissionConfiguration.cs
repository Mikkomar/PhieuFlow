using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        builder.ToTable("FormSubmissions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FormId).IsRequired();
        builder.Property(s => s.FormVersionId).IsRequired();
        builder.Property(s => s.FormVersionNumber).IsRequired();
        builder.Property(s => s.SubmittedAt).IsRequired();

        builder.HasIndex(s => s.FormId);
        builder.HasIndex(s => new { s.FormId, s.FormVersionNumber });

        // Restrict, not Cascade: a submission is a historical record that must outlive the
        // form. WithMany() with no inverse keeps Form and FormVersion free of a collection.
        builder.HasOne(s => s.Form)
            .WithMany()
            .HasForeignKey(s => s.FormId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.FormVersion)
            .WithMany()
            .HasForeignKey(s => s.FormVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Answers)
            .WithOne(a => a.FormSubmission)
            .HasForeignKey(a => a.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

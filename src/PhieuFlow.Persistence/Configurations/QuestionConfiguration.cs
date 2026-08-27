using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.Text).IsRequired().HasMaxLength(1000);
        builder.Property(q => q.Order).IsRequired();

        builder.HasDiscriminator<string>("QuestionType")
            .HasValue<TextAreaQuestion>("TextArea")
            .HasValue<CheckboxQuestion>("Checkbox")
            .HasValue<DropDownQuestion>("DropDown")
            .HasValue<RadioButtonQuestion>("RadioButton")
            .HasValue<CheckBoxGroupQuestion>("CheckBoxGroup")
            .HasValue<NumberQuestion>("Number")
            .HasValue<CalendarQuestion>("Calendar");
    }
}

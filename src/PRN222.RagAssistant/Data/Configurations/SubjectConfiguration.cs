using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Data.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasData(new Subject
        {
            Id = SeedData.Prn222SubjectId,
            Code = "PRN222",
            Name = "PRN222",
            IsActive = true
        });
    }
}

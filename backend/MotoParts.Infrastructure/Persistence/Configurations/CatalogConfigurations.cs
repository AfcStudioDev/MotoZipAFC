using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MotoParts.Domain.Models;

namespace MotoParts.Infrastructure.Persistence.Configurations;

// Классификация: марки, модели, серии, группы и применимость парт-номеров.

public class MotoMarkConfiguration : IEntityTypeConfiguration<MotoMark>
{
    public void Configure(EntityTypeBuilder<MotoMark> builder)
    {
        builder.HasIndex(m => m.Mark).IsUnique();
    }
}

public class MotoModelConfiguration : IEntityTypeConfiguration<MotoModel>
{
    public void Configure(EntityTypeBuilder<MotoModel> builder)
    {
        // Модели остаются при удалении марки — MarkId допускает null.
        builder.HasOne(m => m.Mark)
            .WithMany(m => m.MotoModels)
            .HasForeignKey(m => m.MarkId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

public class PartNumberConfiguration : IEntityTypeConfiguration<PartNumber>
{
    public void Configure(EntityTypeBuilder<PartNumber> builder)
    {
        builder.HasIndex(p => p.PartNum).IsUnique();

        builder.HasOne(p => p.Group)
            .WithMany(g => g.PartNumbers)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PartNumberApplicabilityConfiguration : IEntityTypeConfiguration<PartNumberApplicability>
{
    public void Configure(EntityTypeBuilder<PartNumberApplicability> builder)
    {
        builder.HasOne(a => a.PartNumber)
            .WithMany(p => p.Applicability)
            .HasForeignKey(a => a.PartNumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Model)
            .WithMany(m => m.Applicability)
            .HasForeignKey(a => a.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Одна и та же пара «парт-номер + модель» не должна повторяться.
        builder.HasIndex(a => new { a.PartNumId, a.ModelId }).IsUnique();

        // Отдельный индекс под запрос «все детали, подходящие к этой модели».
        builder.HasIndex(a => a.ModelId);
    }
}

public class PartNumberSeriesApplicabilityConfiguration : IEntityTypeConfiguration<PartNumberSeriesApplicability>
{
    public void Configure(EntityTypeBuilder<PartNumberSeriesApplicability> builder)
    {
        builder.HasOne(a => a.PartNumber)
            .WithMany(p => p.SeriesApplicability)
            .HasForeignKey(a => a.PartNumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Series)
            .WithMany(s => s.Applicability)
            .HasForeignKey(a => a.SeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        // Одна и та же пара «парт-номер + серия» не должна повторяться.
        builder.HasIndex(a => new { a.PartNumId, a.SeriesId }).IsUnique();

        // Отдельный индекс под запрос «все детали этой серии».
        builder.HasIndex(a => a.SeriesId);
    }
}

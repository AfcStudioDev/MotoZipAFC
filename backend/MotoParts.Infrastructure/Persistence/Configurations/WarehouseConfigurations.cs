using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MotoParts.Domain.Models;

namespace MotoParts.Infrastructure.Persistence.Configurations;

// Склад: запчасти, доноры, остатки, фотографии.

public class ZipConfiguration : IEntityTypeConfiguration<Zip>
{
    public void Configure(EntityTypeBuilder<Zip> builder)
    {
        builder.HasOne(z => z.PartNumber)
            .WithMany(p => p.Zips)
            .HasForeignKey(z => z.PartNumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(z => z.IncomeMoto)
            .WithMany(i => i.Zips)
            .HasForeignKey(z => z.IncomeMotoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IncomeMotoConfiguration : IEntityTypeConfiguration<IncomeMoto>
{
    public void Configure(EntityTypeBuilder<IncomeMoto> builder)
    {
        builder.HasOne(i => i.User)
            .WithMany(u => u.IncomeMotos)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StoredConfiguration : IEntityTypeConfiguration<Stored>
{
    public void Configure(EntityTypeBuilder<Stored> builder)
    {
        // Один-к-одному: у детали ровно одна строка остатка.
        builder.HasOne(s => s.Zip)
            .WithOne(z => z.Stored)
            .HasForeignKey<Stored>(s => s.ZipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ZipPhotoConfiguration : IEntityTypeConfiguration<ZipPhoto>
{
    public void Configure(EntityTypeBuilder<ZipPhoto> builder)
    {
        builder.HasOne(p => p.Zip)
            .WithMany(z => z.Photos)
            .HasForeignKey(p => p.ZipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Фотографии всегда выбираются по детали — в Postgres FK не индексируется автоматически.
        builder.HasIndex(p => p.ZipId);
    }
}

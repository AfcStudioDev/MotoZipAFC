using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MotoParts.Domain.Models;

namespace MotoParts.Infrastructure.Persistence.Configurations;

// Справочник операций, журнал движений и история переоценки.

public class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.HasOne(o => o.Type)
            .WithMany(t => t.Operations)
            .HasForeignKey(o => o.TypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> builder)
    {
        // Restrict, а не Cascade: журнал переживает удаление заказа — при удалении
        // OrderId обнуляется, а строка продажи остаётся в истории по детали.
        builder.HasOne(l => l.Order)
            .WithMany(o => o.Logs)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Zip)
            .WithMany(z => z.Logs)
            .HasForeignKey(l => l.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Operation)
            .WithMany(op => op.Logs)
            .HasForeignKey(l => l.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.ZipId, l.CreatedAt });
        builder.HasIndex(l => new { l.OperationId, l.CreatedAt });
        builder.HasIndex(l => l.OrderId);
    }
}

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.HasOne(p => p.Zip)
            .WithMany(z => z.PriceHistory)
            .HasForeignKey(p => p.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Operation)
            .WithMany(op => op.PriceHistory)
            .HasForeignKey(p => p.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ZipId, p.CreatedAt });
    }
}

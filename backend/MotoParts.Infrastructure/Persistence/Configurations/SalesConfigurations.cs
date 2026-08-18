using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MotoParts.Domain.Models;

namespace MotoParts.Infrastructure.Persistence.Configurations;

// Пользователи, адреса, заказы, черновики форм.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class DeliveryAddressConfiguration : IEntityTypeConfiguration<DeliveryAddress>
{
    public void Configure(EntityTypeBuilder<DeliveryAddress> builder)
    {
        builder.HasOne(d => d.User)
            .WithMany(u => u.DeliveryAddresses)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.HasOne(p => p.Address)
            .WithMany(d => d.Purchases)
            .HasForeignKey(p => p.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Purchases)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasOne(o => o.Purchase)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Zip)
            .WithMany(z => z.Orders)
            .HasForeignKey(o => o.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Operation)
            .WithMany(op => op.Orders)
            .HasForeignKey(o => o.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.DeliveryStatus)
            .WithMany(ds => ds.Orders)
            .HasForeignKey(o => o.DeliveryStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserDraftConfiguration : IEntityTypeConfiguration<UserDraft>
{
    public void Configure(EntityTypeBuilder<UserDraft> builder)
    {
        // На пару (пользователь, форма) — не больше одного черновика: сохранение всегда upsert.
        builder.HasIndex(d => new { d.UserId, d.FormKey }).IsUnique();

        // Черновики — служебные данные пользователя, вместе с ним они и должны исчезать.
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

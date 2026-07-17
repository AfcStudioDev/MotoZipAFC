using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Models;

namespace MotoParts.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MotoMark> MotoMarks => Set<MotoMark>();
    public DbSet<PartNumber> PartNumbers => Set<PartNumber>();
    public DbSet<ZipGroup> ZipGroups => Set<ZipGroup>();
    public DbSet<MotoModel> MotoModels => Set<MotoModel>();
    public DbSet<Zip> Zip => Set<Zip>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DeliveryAddress> DeliveryAddresses => Set<DeliveryAddress>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ---------- MotoMarks ----------
        mb.Entity<MotoMark>(e =>
        {
            e.ToTable("MotoMarks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.Mark).HasColumnName("Mark");
            e.HasIndex(x => x.Mark).IsUnique();
        });

        // ---------- PartNumbers ----------
        mb.Entity<PartNumber>(e =>
        {
            e.ToTable("PartNumbers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.Number).HasColumnName("PartNumber");
            e.HasIndex(x => x.Number).IsUnique();
        });

        // ---------- ZipGroups ----------
        mb.Entity<ZipGroup>(e =>
        {
            e.ToTable("ZipGroups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.GroupName).HasColumnName("GroupName");
        });

        // ---------- MotoModels ----------
        mb.Entity<MotoModel>(e =>
        {
            e.ToTable("MotoModels");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.MarkId).HasColumnName("MarkId");
            e.Property(x => x.Model).HasColumnName("Model");

            e.HasOne(x => x.Mark)
                .WithMany(m => m.Models)
                .HasForeignKey(x => x.MarkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Zip ----------
        mb.Entity<Zip>(e =>
        {
            e.ToTable("Zip");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("Name").IsRequired();
            e.Property(x => x.Cost).HasColumnName("Cost").HasColumnType("numeric");
            e.Property(x => x.PartNumberId).HasColumnName("PartNumberId");
            e.Property(x => x.MarkId).HasColumnName("MarkId");
            e.Property(x => x.ModelId).HasColumnName("ModelId");
            e.Property(x => x.GroupId).HasColumnName("GroupId");
            e.Property(x => x.CountStored).HasColumnName("CountStored").HasDefaultValue(0);
            e.Property(x => x.Year).HasColumnName("Year").HasColumnType("date");

            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.PartNumber).WithMany().HasForeignKey(x => x.PartNumberId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Mark).WithMany().HasForeignKey(x => x.MarkId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------- Orders ----------
        mb.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderNumber).HasColumnName("OrderNumber").IsRequired();
            e.Property(x => x.CountOrdered).HasColumnName("CountOrdered").HasDefaultValue(0);
            e.Property(x => x.NomenclatureId).HasColumnName("NomenclatureId");
            e.Property(x => x.AddressId).HasColumnName("AddressId");
            e.Property(x => x.OrderDateTime).HasColumnName("OrderDateTime").HasColumnType("timestamptz");

            e.HasOne(x => x.Nomenclature).WithMany().HasForeignKey(x => x.NomenclatureId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Address).WithMany(a => a.Orders).HasForeignKey(x => x.AddressId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------- Users ----------
        mb.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.Email).HasColumnName("Email").IsRequired();
            e.Property(x => x.IsAdmin).HasColumnName("IsAdmin").HasDefaultValue(false);
            e.Property(x => x.FIO).HasColumnName("FIO").IsRequired();
            e.Property(x => x.PhoneNumber).HasColumnName("PhoneNumber");
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ---------- DeliveryAdressess (имя сохранено как в исходной схеме) ----------
        mb.Entity<DeliveryAddress>(e =>
        {
            e.ToTable("DeliveryAdressess");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.Address).HasColumnName("Address").IsRequired();
            e.Property(x => x.PostCode).HasColumnName("PostCode");
            e.Property(x => x.UserId).HasColumnName("UserId");

            e.HasOne(x => x.User).WithMany(u => u.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });

        // ---------- Payments (расширение для ЮKassa) ----------
        mb.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("numeric");
            e.HasIndex(x => x.YooKassaPaymentId).IsUnique();
            e.HasOne(x => x.Order).WithOne(o => o.Payment).HasForeignKey<Payment>(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

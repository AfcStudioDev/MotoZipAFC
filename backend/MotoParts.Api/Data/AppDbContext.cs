using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Models;

namespace MotoParts.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MotoMark> MotoMarks { get; set; }
    public DbSet<PartNumber> PartNumbers { get; set; }
    public DbSet<ZipGroup> ZipGroups { get; set; }
    public DbSet<MotoModel> MotoModels { get; set; }
    public DbSet<Zip> Zips { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DeliveryAddress> DeliveryAddressess { get; set; } // Имя таблицы по DBML
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Stored> Stored { get; set; }
    public DbSet<IncomeMoto> IncomeMotos { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<DeliveryStatus> DeliveryStatuses { get; set; }

    // Если Payment нужен, раскомментируйте:
    // public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Уникальные индексы (Unique)
        modelBuilder.Entity<MotoMark>().HasIndex(m => m.Mark).IsUnique();
        modelBuilder.Entity<PartNumber>().HasIndex(p => p.PartNum).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // ----------------------------------------------------
        // НАСТРОЙКА СВЯЗЕЙ СТРОГО ПО СХЕМЕ DBML
        // ----------------------------------------------------

        // fk_MotoMark_id_MotoModels [ delete: set null ]
        modelBuilder.Entity<MotoModel>()
            .HasOne(m => m.Mark)
            .WithMany(m => m.MotoModels)
            .HasForeignKey(m => m.MarkId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_ZipGroups_id_Zip [ delete: set null ]
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Group)
            .WithMany(g => g.Zips)
            .HasForeignKey(z => z.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_MotoModels_id_Zip [ delete: set null ]
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Model)
            .WithMany(m => m.Zips)
            .HasForeignKey(z => z.ModelId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_PartNumbers_id_Zip [ delete: no action ]
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.PartNumber)
            .WithMany(p => p.Zips)
            .HasForeignKey(z => z.PartNumId) // Используем переименованное поле
            .OnDelete(DeleteBehavior.Restrict);

        // fk_MotoMarks_id_Zip [ delete: no action ]
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Mark)
            .WithMany(m => m.Zips)
            .HasForeignKey(z => z.MarkId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Users_id_DeliveryAdressess [ delete: no action ]
        modelBuilder.Entity<DeliveryAddress>()
            .HasOne(d => d.User)
            .WithMany(u => u.DeliveryAddresses)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_DeliveryAdressess_id_Orders [ delete: no action ]
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Address)
            .WithMany(d => d.Orders)
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Operations_id_Orders [ delete: no action ]
        modelBuilder.Entity<Order>()
            .HasOne(o => o.OperationType)
            .WithMany(op => op.Orders)
            .HasForeignKey(o => o.OperationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Zip_id_Stored [ delete: no action ]
        modelBuilder.Entity<Stored>()
            .HasOne(s => s.Zip)
            .WithMany(z => z.StoredItems)
            .HasForeignKey(s => s.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_IncomeMoto_id_Zip [ delete: no action ]
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.IncomeMoto)
            .WithMany(i => i.Zips)
            .HasForeignKey(z => z.IncomeMotoId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Zip_id_Orders [ delete: no action ]
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Nomenclature)
            .WithMany(z => z.Orders)
            .HasForeignKey(o => o.NomenclatureId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Orders_id_Log [ delete: no action ]
        modelBuilder.Entity<Log>()
            .HasOne(l => l.Order)
            .WithMany(o => o.Logs)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_DeliveryStatuses_id_Orders [ delete: no action ]
        modelBuilder.Entity<Order>()
            .HasOne(o => o.DeliveryStatus)
            .WithMany(ds => ds.Orders)
            .HasForeignKey(o => o.DeliveryStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
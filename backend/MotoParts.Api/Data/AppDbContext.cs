using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Models;

namespace MotoParts.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MotoMark> MotoMarks { get; set; }
    public DbSet<PartNumber> PartNumbers { get; set; }
    public DbSet<PartNumberApplicability> PartNumberApplicabilities { get; set; }
    public DbSet<MotoSeries> MotoSeries { get; set; }
    public DbSet<PartNumberSeriesApplicability> PartNumberSeriesApplicabilities { get; set; }
    public DbSet<ZipGroup> ZipGroups { get; set; }
    public DbSet<MotoModel> MotoModels { get; set; }
    public DbSet<Zip> Zips { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DeliveryAddress> DeliveryAddressess { get; set; } // Имя таблицы по DBML
    public DbSet<OperationType> OperationTypes { get; set; }
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Stored> Stored { get; set; }
    public DbSet<IncomeMoto> IncomeMotos { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<DeliveryStatus> DeliveryStatuses { get; set; }
    public DbSet<ZipPhoto> ZipPhotos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ----------------------------------------------------
        // УНИКАЛЬНЫЕ ИНДЕКСЫ
        // ----------------------------------------------------
        modelBuilder.Entity<MotoMark>().HasIndex(m => m.Mark).IsUnique();
        modelBuilder.Entity<PartNumber>().HasIndex(p => p.PartNum).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // ----------------------------------------------------
        // СПРАВОЧНИКИ КЛАССИФИКАЦИИ
        // ----------------------------------------------------

        // MotoMarks.id < MotoModels.MarkId [ delete: set null ]
        modelBuilder.Entity<MotoModel>()
            .HasOne(m => m.Mark)
            .WithMany(m => m.MotoModels)
            .HasForeignKey(m => m.MarkId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // ZipGroups.id < PartNumbers.GroupId
        modelBuilder.Entity<PartNumber>()
            .HasOne(p => p.Group)
            .WithMany(g => g.PartNumbers)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // ----------------------------------------------------
        // ПРИМЕНИМОСТЬ ПАРТ-НОМЕРА К МОДЕЛЯМ (многие-ко-многим)
        // ----------------------------------------------------

        // PartNumbers.id < PartNumberApplicability.PartNumId [ delete: cascade ]
        modelBuilder.Entity<PartNumberApplicability>()
            .HasOne(a => a.PartNumber)
            .WithMany(p => p.Applicability)
            .HasForeignKey(a => a.PartNumId)
            .OnDelete(DeleteBehavior.Cascade);

        // MotoModels.id < PartNumberApplicability.ModelId [ delete: no action ]
        modelBuilder.Entity<PartNumberApplicability>()
            .HasOne(a => a.Model)
            .WithMany(m => m.Applicability)
            .HasForeignKey(a => a.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Одна и та же пара «парт-номер + модель» не должна повторяться.
        modelBuilder.Entity<PartNumberApplicability>()
            .HasIndex(a => new { a.PartNumId, a.ModelId })
            .IsUnique();

        // Отдельный индекс под запрос «все детали, подходящие к этой модели».
        modelBuilder.Entity<PartNumberApplicability>()
            .HasIndex(a => a.ModelId);

        // ----------------------------------------------------
        // ПРИМЕНИМОСТЬ ПАРТ-НОМЕРА К СЕРИЯМ (многие-ко-многим, независимо от моделей)
        // ----------------------------------------------------

        // PartNumbers.id < PartNumberSeriesApplicability.PartNumId [ delete: cascade ]
        modelBuilder.Entity<PartNumberSeriesApplicability>()
            .HasOne(a => a.PartNumber)
            .WithMany(p => p.SeriesApplicability)
            .HasForeignKey(a => a.PartNumId)
            .OnDelete(DeleteBehavior.Cascade);

        // MotoSeries.id < PartNumberSeriesApplicability.SeriesId [ delete: no action ]
        modelBuilder.Entity<PartNumberSeriesApplicability>()
            .HasOne(a => a.Series)
            .WithMany(s => s.Applicability)
            .HasForeignKey(a => a.SeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        // Одна и та же пара «парт-номер + серия» не должна повторяться.
        modelBuilder.Entity<PartNumberSeriesApplicability>()
            .HasIndex(a => new { a.PartNumId, a.SeriesId })
            .IsUnique();

        // Отдельный индекс под запрос «все детали этой серии».
        modelBuilder.Entity<PartNumberSeriesApplicability>()
            .HasIndex(a => a.SeriesId);

        // ----------------------------------------------------
        // ЗАПЧАСТИ
        // ----------------------------------------------------

        // PartNumbers.id < Zip.PartNumId
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.PartNumber)
            .WithMany(p => p.Zips)
            .HasForeignKey(z => z.PartNumId)
            .OnDelete(DeleteBehavior.Restrict);

        // IncomeMoto.id < Zip.IncomeMotoId
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.IncomeMoto)
            .WithMany(i => i.Zips)
            .HasForeignKey(z => z.IncomeMotoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Users.id < IncomeMoto.UserId
        modelBuilder.Entity<IncomeMoto>()
            .HasOne(i => i.User)
            .WithMany(u => u.IncomeMotos)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zip.id < Stored.ZipId — один-к-одному, ZipId уникален
        modelBuilder.Entity<Stored>()
            .HasOne(s => s.Zip)
            .WithOne(z => z.Stored)
            .HasForeignKey<Stored>(s => s.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zip.id < ZipPhotos.ZipId
        modelBuilder.Entity<ZipPhoto>()
            .HasOne(p => p.Zip)
            .WithMany(z => z.Photos)
            .HasForeignKey(p => p.ZipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Фотографии всегда выбираются по детали — в Postgres FK не индексируется автоматически.
        modelBuilder.Entity<ZipPhoto>().HasIndex(p => p.ZipId);

        // ----------------------------------------------------
        // ПОЛЬЗОВАТЕЛИ, АДРЕСА, ЗАКАЗЫ
        // ----------------------------------------------------

        // Users.id < DeliveryAdressess.UserId
        modelBuilder.Entity<DeliveryAddress>()
            .HasOne(d => d.User)
            .WithMany(u => u.DeliveryAddresses)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // DeliveryAdressess.id < Orders.AddressId
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Address)
            .WithMany(d => d.Orders)
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // Users.id < Orders.UserId
        modelBuilder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zip.id < Orders.ZipId
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Zip)
            .WithMany(z => z.Orders)
            .HasForeignKey(o => o.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Operations.id < Orders.OperationId
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Operation)
            .WithMany(op => op.Orders)
            .HasForeignKey(o => o.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // DeliveryStatuses.id < Orders.DeliveryStatusId
        modelBuilder.Entity<Order>()
            .HasOne(o => o.DeliveryStatus)
            .WithMany(ds => ds.Orders)
            .HasForeignKey(o => o.DeliveryStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // ----------------------------------------------------
        // СПРАВОЧНИК ОПЕРАЦИЙ
        // ----------------------------------------------------

        // OperationType.id < Operations.TypeId
        modelBuilder.Entity<Operation>()
            .HasOne(o => o.Type)
            .WithMany(t => t.Operations)
            .HasForeignKey(o => o.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ----------------------------------------------------
        // ЖУРНАЛ ОПЕРАЦИЙ
        // ----------------------------------------------------

        // Orders.id < Log.OrderId
        modelBuilder.Entity<Log>()
            .HasOne(l => l.Order)
            .WithMany(o => o.Logs)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zip.id < Log.ZipId
        modelBuilder.Entity<Log>()
            .HasOne(l => l.Zip)
            .WithMany(z => z.Logs)
            .HasForeignKey(l => l.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Users.id < Log.UserId
        modelBuilder.Entity<Log>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Operations.id < Log.OperationId
        modelBuilder.Entity<Log>()
            .HasOne(l => l.Operation)
            .WithMany(op => op.Logs)
            .HasForeignKey(l => l.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Log>().HasIndex(l => new { l.ZipId, l.CreatedAt });
        modelBuilder.Entity<Log>().HasIndex(l => new { l.OperationId, l.CreatedAt });
        modelBuilder.Entity<Log>().HasIndex(l => l.OrderId);

        // ----------------------------------------------------
        // ИСТОРИЯ ПЕРЕОЦЕНКИ
        // ----------------------------------------------------

        // Zip.id < PriceHistory.ZipId
        modelBuilder.Entity<PriceHistory>()
            .HasOne(p => p.Zip)
            .WithMany(z => z.PriceHistory)
            .HasForeignKey(p => p.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Operations.id < PriceHistory.OperationId
        modelBuilder.Entity<PriceHistory>()
            .HasOne(p => p.Operation)
            .WithMany(op => op.PriceHistory)
            .HasForeignKey(p => p.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Users.id < PriceHistory.UserId
        modelBuilder.Entity<PriceHistory>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PriceHistory>().HasIndex(p => new { p.ZipId, p.CreatedAt });
    }
}

using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Models;

using System.Reflection.Emit;

namespace MotoParts.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MotoMark> MotoMarks { get; set; }
    public DbSet<PartNumber> PartNumbers { get; set; }
    public DbSet<ZipGroup> ZipGroups { get; set; }
    public DbSet<MotoModel> MotoModels { get; set; }
    public DbSet<Zip> Zips { get; set; }
    public DbSet<Movement> Movements { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DeliveryAdress> DeliveryAdresses { get; set; }
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Stored> Stored { get; set; }
    public DbSet<IncomeMoto> IncomeMotos { get; set; }
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 1. Уникальные индексы (Unique)
        modelBuilder.Entity<MotoMark>().HasIndex(m => m.Mark).IsUnique();
        modelBuilder.Entity<PartNumber>().HasIndex(p => p.PartNum).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // 2. Настройка связей (Foreign Keys) и каскадного удаления согласно схеме

        // fk_Марки мотоциклов_id_МоделиМотоциклов (delete: set null)
        modelBuilder.Entity<MotoModel>()
            .HasOne(m => m.Mark)
            .WithMany(m => m.MotoModels)
            .HasForeignKey(m => m.MarkId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_ГруппыЗапЧастей_id_ЗапЧасти (delete: set null)
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Group)
            .WithMany(g => g.Zips)
            .HasForeignKey(z => z.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_МоделиМотоциклов_id_ЗапЧасти (delete: set null)
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Model)
            .WithMany(m => m.Zips)
            .HasForeignKey(z => z.ModelId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // fk_ПартНомера_id_ЗапЧасти (delete: no action)
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.PartNumber)
            .WithMany(p => p.Zips)
            .HasForeignKey(z => z.PartNumberId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Марки мотоциклов_id_ЗапЧасти (delete: no action)
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.Mark)
            .WithMany(m => m.Zips)
            .HasForeignKey(z => z.MarkId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Пользователи_id_АдресаДоставки (delete: no action)
        modelBuilder.Entity<DeliveryAdress>()
            .HasOne(d => d.User)
            .WithMany(u => u.DeliveryAddresses)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_АдресаДоставки_id_Заказы (delete: no action)
        modelBuilder.Entity<Movement>()
            .HasOne(m => m.Address)
            .WithMany(d => d.Movements)
            .HasForeignKey(m => m.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Operations_id_Movements (delete: no action)
        modelBuilder.Entity<Movement>()
            .HasOne(m => m.OperationType)
            .WithMany(o => o.Movements)
            .HasForeignKey(m => m.OperationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Zip_id_Stored (delete: no action)
        modelBuilder.Entity<Stored>()
            .HasOne(s => s.Zip)
            .WithMany(z => z.StoredItems)
            .HasForeignKey(s => s.ZipId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_IncomeMoto_id_Zip (delete: no action)
        modelBuilder.Entity<Zip>()
            .HasOne(z => z.IncomeMoto)
            .WithMany(i => i.Zips)
            .HasForeignKey(z => z.IncomeMotoId)
            .OnDelete(DeleteBehavior.Restrict);

        // fk_Zip_id_Movements (delete: no action)
        modelBuilder.Entity<Movement>()
            .HasOne(m => m.Nomenclature)
            .WithMany(z => z.Movements)
            .HasForeignKey(m => m.NomenclatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

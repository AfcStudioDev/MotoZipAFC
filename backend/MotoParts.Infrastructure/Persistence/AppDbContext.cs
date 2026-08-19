using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using MotoParts.Application.Abstractions;
using MotoParts.Domain.Models;

namespace MotoParts.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
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
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<DeliveryStatus> DeliveryStatuses { get; set; }
    public DbSet<ZipPhoto> ZipPhotos { get; set; }
    public DbSet<UserDraft> UserDrafts { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<SupportMessage> SupportMessages { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Раньше здесь лежали ~260 строк настройки всех сущностей подряд. Теперь каждая
        // сущность описана своим IEntityTypeConfiguration в Persistence/Configurations —
        // они подхватываются из сборки автоматически.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQLite (используется только в тестах — в бою всегда Postgres) не умеет ORDER BY
        // по DateTimeOffset напрямую. Постгрес это переводит сам, поэтому конвертер нужен
        // только здесь и только для этого провайдера — на прод-запросы не влияет.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var converter = new DateTimeOffsetToBinaryConverter();
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(converter);
                }
            }
        }
    }
}

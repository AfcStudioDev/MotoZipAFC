using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

namespace MotoParts.Application.Tests;

/// <summary>
/// Отдельная база на каждый тест: SQLite in-memory живёт, пока открыто соединение.
/// Берём настоящий AppDbContext, а не подделку, — так проверяются и конфигурации EF.
/// </summary>
public sealed class WarehouseFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly User _user;
    private readonly DeliveryAddress _address;

    public AppDbContext Db { get; }

    /// <summary>Деталь, вокруг которой крутятся тесты склада.</summary>
    public Zip Zip { get; }

    public WarehouseFixture(int initialStock = 10)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();

        // Минимальный набор справочников, без которого не проходят внешние ключи журнала.
        Db.OperationTypes.Add(new OperationType { Id = 1, Description = "Движение товара" });
        foreach (var (id, name) in new[]
                 {
                     ((short)OperationEnum.Income, "Приход"),
                     ((short)OperationEnum.Sale, "Продажа"),
                     ((short)OperationEnum.Refund, "Возврат"),
                     ((short)OperationEnum.WriteOff, "Списание"),
                     ((short)OperationEnum.Correction, "Коррекция остатка"),
                 })
        {
            Db.Operations.Add(new Operation { Id = id, TypeId = 1, Description = name });
        }

        Db.DeliveryStatuses.Add(new DeliveryStatus { Id = (short)DeliveryStatusEnum.created, Description = "created" });

        var group = new ZipGroup { Id = 1, GroupName = "Двигатель" };
        var partNumber = new PartNumber { Id = 1, PartNum = "15410-MFJ-D01", Name = "Масляный фильтр", GroupId = 1 };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda" };
        Db.ZipGroups.Add(group);
        Db.PartNumbers.Add(partNumber);
        Db.IncomeMotos.Add(donor);

        _user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        _address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = _user };
        Db.Users.Add(_user);
        Db.DeliveryAddressess.Add(_address);

        Zip = new Zip
        {
            Id = Guid.NewGuid(),
            PartNumId = partNumber.Id,
            IncomeMotoId = donor.Id,
            IncomeCost = 1250m,
            SellCost = 1790m,
            IncomeDate = new DateOnly(2026, 1, 1)
        };
        Db.Zips.Add(Zip);
        Db.Stored.Add(new Stored { ZipId = Zip.Id, Count = initialStock });

        Db.SaveChanges();
    }

    /// <summary>
    /// Настоящая строка заказа вместе с её покупкой. Нужна, потому что журнал ссылается на
    /// заказ по внешнему ключу: в бою заказ и движение товара сохраняются одной транзакцией,
    /// и тест должен вести себя так же.
    /// </summary>
    public Order AddOrder(int qty, decimal sellCost = 1500m, string orderNumber = "ORD-TEST")
    {
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-TEST-" + orderNumber,
            UserId = _user.Id,
            AddressId = _address.Id,
            OrderDateTime = DateTimeOffset.UtcNow
        };
        Db.Purchases.Add(purchase);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CountOrdered = qty,
            ZipId = Zip.Id,
            PurchaseId = purchase.Id,
            OrderDateTime = purchase.OrderDateTime,
            SellCost = sellCost,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        };
        Db.Orders.Add(order);
        Db.SaveChanges();
        return order;
    }

    public int StockOf(Guid zipId) =>
        Db.Stored.AsNoTracking().Single(s => s.ZipId == zipId).Count;

    public List<Log> LogsFor(Guid zipId) =>
        Db.Logs.AsNoTracking().Where(l => l.ZipId == zipId).ToList();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

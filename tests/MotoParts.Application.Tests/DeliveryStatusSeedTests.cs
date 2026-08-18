using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using Xunit;

namespace MotoParts.Application.Tests;

/// <summary>
/// Регрессия на самую дорогую ошибку проекта: DeliveryStatusEnum был 0-based (created = 0),
/// а DbSeeder заводил статусы с Id начиная с 1. Код писал в заказ несуществующий статус 0,
/// заказ оставался с DeliveryStatusId = null и не попадал ни в одну вкладку «Отправлений» —
/// внешне выглядело как «заказы просто пропадают».
///
/// Тест держит enum и сид в одной системе координат: если кто-то поменяет одно, не поменяв
/// второе, это упадёт здесь, а не в проде через неделю.
/// </summary>
public class DeliveryStatusSeedTests
{
    private static AppDbContext NewDb(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Значения_enum_совпадают_с_тем_что_заводит_сидер()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = NewDb(connection);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        await DbSeeder.SeedAsync(db, config);

        var seeded = await db.DeliveryStatuses.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Description);

        foreach (var value in Enum.GetValues<DeliveryStatusEnum>())
        {
            var id = (short)value;
            Assert.True(seeded.ContainsKey(id),
                $"Статус {value} = {id} не заведён сидером — заказ с ним не пройдёт по внешнему ключу");
            Assert.Equal(value.ToString(), seeded[id]);
        }
    }

    [Fact]
    public void Ни_один_статус_доставки_не_равен_нулю()
    {
        // Id в БД начинаются с 1; значение 0 означало бы, что enum снова разъехался с сидом.
        foreach (var value in Enum.GetValues<DeliveryStatusEnum>())
            Assert.NotEqual(0, (short)value);
    }

    [Fact]
    public async Task Заказ_со_статусом_created_проходит_по_внешнему_ключу()
    {
        // Именно на этом ломались гостевые заказы: статус выставлялся, но такого Id в БД не было.
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = NewDb(connection);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        await DbSeeder.SeedAsync(db, config);

        var user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        var address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = user };
        var group = new ZipGroup { GroupName = "Двигатель" };
        var pn = new PartNumber { PartNum = "TEST-1", Name = "Деталь", Group = group };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Донор" };
        var zip = new Zip
        {
            Id = Guid.NewGuid(), PartNumber = pn, IncomeMoto = donor,
            IncomeCost = 100m, SellCost = 200m, IncomeDate = new DateOnly(2026, 1, 1)
        };
        db.AddRange(user, address, group, pn, donor, zip);
        await db.SaveChangesAsync();

        db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-FK-CHECK",
            CountOrdered = 1,
            ZipId = zip.Id,
            AddressId = address.Id,
            UserId = user.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 200m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        });

        // Если enum разъедется с сидом — здесь будет нарушение внешнего ключа.
        await db.SaveChangesAsync();

        var saved = await db.Orders.Include(o => o.DeliveryStatus)
            .SingleAsync(o => o.OrderNumber == "ORD-FK-CHECK");
        Assert.Equal("created", saved.DeliveryStatus!.Description);
    }
}

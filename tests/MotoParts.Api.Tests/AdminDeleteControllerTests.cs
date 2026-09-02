using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Controllers;
using MotoParts.Application.Warehouse;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Регрессия: удаление последнего Order покупки должно забирать с собой и пустую Purchase.
/// Раньше Purchase (адрес/оплата/доставка) не удалялась вместе с последней позицией — она
/// пропадала из вкладки «Заказы» (там показываются строки Order), но продолжала висеть в базе
/// и ссылаться на адрес, из-за чего DeleteEntity("addressess", ...) отвечал "На этот адрес
/// оформлены заказы: N" даже когда в админ-панели не было видно ни одного заказа.
/// </summary>
public sealed class AdminDeleteControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly DeliveryAddress _address;

    public AdminDeleteControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.OperationTypes.Add(new OperationType { Id = 1, Description = "Движение товара" });
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Sale, TypeId = 1, Description = "Продажа" });
        // Незавершённый (null-статус) заказ при удалении возвращает остаток на склад — это пишет
        // лог с типом Refund, поэтому справочник операций нужен и для него, не только для Sale.
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Refund, TypeId = 1, Description = "Возврат" });

        var group = new ZipGroup { Id = 1, GroupName = "Двигатель" };
        var partNumber = new PartNumber { Id = 1, PartNum = "15410-MFJ-D01", Name = "Масляный фильтр", GroupId = 1 };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda" };
        _db.ZipGroups.Add(group);
        _db.PartNumbers.Add(partNumber);
        _db.IncomeMotos.Add(donor);

        var user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        _address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = user };
        _db.Users.Add(user);
        _db.DeliveryAddressess.Add(_address);

        var zip = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber.Id, IncomeMotoId = donor.Id, IncomeCost = 1250m, SellCost = 1790m };
        _db.Zips.Add(zip);
        _db.Stored.Add(new Stored { ZipId = zip.Id, Count = 5 });
        _db.SaveChanges();

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-TEST",
            UserId = user.Id,
            AddressId = _address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
        };
        _db.Purchases.Add(purchase);

        _db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST",
            CountOrdered = 1,
            ZipId = zip.Id,
            PurchaseId = purchase.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 1790m,
            OperationId = (short)OperationEnum.Sale,
        });
        _db.SaveChanges();
    }

    private AdminDeleteController MakeController()
    {
        var controller = new AdminDeleteController(_db, new WarehouseService(_db));
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    [Fact]
    public async Task Удаление_последнего_заказа_покупки_удаляет_и_саму_покупку()
    {
        var controller = MakeController();
        var orderId = await _db.Orders.Select(o => o.Id).SingleAsync();

        var result = await controller.DeleteEntity("orders", orderId.ToString());

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(await _db.Orders.ToListAsync());
        Assert.Empty(await _db.Purchases.ToListAsync());
    }

    [Fact]
    public async Task После_удаления_последнего_заказа_адрес_можно_удалить()
    {
        var controller = MakeController();
        var orderId = await _db.Orders.Select(o => o.Id).SingleAsync();
        await controller.DeleteEntity("orders", orderId.ToString());

        var result = await controller.DeleteEntity("addressess", _address.Id.ToString());

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(await _db.DeliveryAddressess.FindAsync(_address.Id));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

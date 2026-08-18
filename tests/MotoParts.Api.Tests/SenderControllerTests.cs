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
/// Пока онлайн-оплата отключена (PaymentsController закомментирован), «оплачено» —
/// это флаг, который вручную ставит администратор. Отправитель не должен иметь возможность
/// продвинуть неоплаченный заказ дальше «Отменить» / «Не отправлено» — иначе товар может
/// уехать клиенту, за который не заплатили.
/// </summary>
public sealed class SenderControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly Order _order;
    private readonly Purchase _purchase;

    public SenderControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.OperationTypes.Add(new OperationType { Id = 1, Description = "Движение товара" });
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Sale, TypeId = 1, Description = "Продажа" });
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Refund, TypeId = 1, Description = "Возврат" });
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Other, TypeId = 1, Description = "Другое" });

        foreach (var status in Enum.GetValues<DeliveryStatusEnum>())
            _db.DeliveryStatuses.Add(new DeliveryStatus { Id = (short)status, Description = status.ToString() });

        var group = new ZipGroup { Id = 1, GroupName = "Двигатель" };
        var partNumber = new PartNumber { Id = 1, PartNum = "15410-MFJ-D01", Name = "Масляный фильтр", GroupId = 1 };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda" };
        _db.ZipGroups.Add(group);
        _db.PartNumbers.Add(partNumber);
        _db.IncomeMotos.Add(donor);

        var user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        var address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = user };
        _db.Users.Add(user);
        _db.DeliveryAddressess.Add(address);

        var zip = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber.Id, IncomeMotoId = donor.Id, IncomeCost = 1250m, SellCost = 1790m };
        _db.Zips.Add(zip);
        _db.Stored.Add(new Stored { ZipId = zip.Id, Count = 5 });
        _db.SaveChanges();

        _purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-TEST",
            UserId = user.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            IsPaid = false
        };
        _db.Purchases.Add(_purchase);

        _order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST",
            CountOrdered = 2,
            ZipId = zip.Id,
            PurchaseId = _purchase.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 1790m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        };
        _db.Orders.Add(_order);
        _db.SaveChanges();
    }

    private SenderController MakeController()
    {
        var controller = new SenderController(_db, new WarehouseService(_db));
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("completed")]
    public async Task Неоплаченный_заказ_нельзя_продвинуть_вперёд(string targetStatus)
    {
        var controller = MakeController();

        var result = await controller.UpdateStatus(_order.Id, new UpdateStatusDto { Status = targetStatus });

        Assert.IsType<BadRequestObjectResult>(result);
        var reloaded = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order.Id);
        Assert.Equal((short)DeliveryStatusEnum.created, reloaded.DeliveryStatusId);
    }

    [Fact]
    public async Task Неоплаченный_заказ_всё_равно_можно_отменить()
    {
        var controller = MakeController();

        var result = await controller.UpdateStatus(_order.Id, new UpdateStatusDto { Status = "canceled" });

        Assert.IsType<OkObjectResult>(result);
        var reloaded = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order.Id);
        Assert.Equal((short)DeliveryStatusEnum.canceled, reloaded.DeliveryStatusId);
    }

    [Fact]
    public async Task Оплаченный_заказ_можно_отправить()
    {
        _purchase.IsPaid = true;
        await _db.SaveChangesAsync();
        var controller = MakeController();

        var result = await controller.UpdateStatus(_order.Id, new UpdateStatusDto { Status = "sent" });

        Assert.IsType<OkObjectResult>(result);
        var reloaded = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order.Id);
        Assert.Equal((short)DeliveryStatusEnum.sent, reloaded.DeliveryStatusId);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

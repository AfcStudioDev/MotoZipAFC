using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Controllers;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Просмотр уже оформленных заказов. Оформление — см. PurchasesControllerTests
/// (там же и регрессия на цикл сериализации при указании адреса доставки).
/// </summary>
public sealed class OrdersControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly User _user;
    private readonly DeliveryAddress _address;
    private readonly Zip _zip;

    public OrdersControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.OperationTypes.Add(new OperationType { Id = 1, Description = "Движение товара" });
        _db.Operations.Add(new Operation { Id = (short)OperationEnum.Sale, TypeId = 1, Description = "Продажа" });
        _db.DeliveryStatuses.Add(new DeliveryStatus { Id = (short)DeliveryStatusEnum.created, Description = "created" });

        var group = new ZipGroup { Id = 1, GroupName = "Двигатель" };
        var partNumber = new PartNumber { Id = 1, PartNum = "15410-MFJ-D01", Name = "Масляный фильтр", GroupId = 1 };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda" };
        _db.ZipGroups.Add(group);
        _db.PartNumbers.Add(partNumber);
        _db.IncomeMotos.Add(donor);

        _user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        _address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = _user };
        _db.Users.Add(_user);
        _db.DeliveryAddressess.Add(_address);

        _zip = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber.Id, IncomeMotoId = donor.Id, IncomeCost = 1250m, SellCost = 1790m };
        _db.Zips.Add(_zip);
        _db.Stored.Add(new Stored { ZipId = _zip.Id, Count = 5 });

        _db.SaveChanges();
    }

    private OrdersController MakeController()
    {
        var controller = new OrdersController(_db);
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    /// <summary>Заказ вместе с его покупкой — само оформление не проверяется здесь.</summary>
    private Order SeedOrder(bool isPaid = false, string? receiptFileName = null)
    {
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-MY-TEST",
            UserId = _user.Id,
            AddressId = _address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            IsPaid = isPaid,
            ReceiptFileName = receiptFileName
        };
        _db.Purchases.Add(purchase);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-MY-TEST",
            CountOrdered = 1,
            ZipId = _zip.Id,
            PurchaseId = purchase.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 1790m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        };
        _db.Orders.Add(order);
        _db.SaveChanges();
        return order;
    }

    [Fact]
    public async Task Мои_заказы_отдают_данные_покупки_на_каждой_позиции()
    {
        SeedOrder(isPaid: true, receiptFileName: "receipt.jpg");
        var controller = MakeController();

        var result = await controller.My();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<OrderDto>>(ok.Value);
        var item = Assert.Single(page.Items);

        Assert.True(item.IsPaid);
        Assert.Equal("receipt.jpg", item.ReceiptFileName);
        Assert.Equal("ул. Тестовая, 1", item.Address);
        Assert.StartsWith("PUR-", item.PurchaseNumber);
    }

    [Fact]
    public async Task Чужие_заказы_не_попадают_в_список()
    {
        SeedOrder();
        var otherUser = new User { Email = "other@test.local", FIO = "Другой" };
        _db.Users.Add(otherUser);
        _db.SaveChanges();

        var controller = new OrdersController(_db);
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, otherUser.Id.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };

        var result = await controller.My();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<OrderDto>>(ok.Value);
        Assert.Empty(page.Items);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

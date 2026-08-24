using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Controllers;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using VkChatBot;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>Ничего не отправляет — тестам сама отправка в VK не интересна, важно лишь не падать.</summary>
file sealed class FakeVkBotService : IVkBotService
{
    public void SendMessage(string message) { }
}

/// <summary>
/// Создание обращения в поддержку: заказ и тема — по одному из двух (см. SupportTicket).
/// Обращение может быть не привязано к заказу, если у него есть тема.
/// </summary>
public sealed class SupportControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly User _user;
    private readonly Order _order;

    public SupportControllerTests()
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
        var address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = _user };
        _db.Users.Add(_user);
        _db.DeliveryAddressess.Add(address);

        var zip = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber.Id, IncomeMotoId = donor.Id, IncomeCost = 1250m, SellCost = 1790m };
        _db.Zips.Add(zip);
        _db.Stored.Add(new Stored { ZipId = zip.Id, Count = 5 });
        _db.SaveChanges();

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-TEST",
            UserId = _user.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
        };
        _db.Purchases.Add(purchase);

        _order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST",
            CountOrdered = 1,
            ZipId = zip.Id,
            PurchaseId = purchase.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 1790m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created,
        };
        _db.Orders.Add(_order);
        _db.SaveChanges();
    }

    private SupportController MakeController()
    {
        var controller = new SupportController(_db, new FakeVkBotService());
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    [Fact]
    public async Task Обращение_с_заказом_без_темы_создаётся()
    {
        var controller = MakeController();
        var request = new CreateSupportTicketRequest(_order.Id, Subject: null, Message: "Вопрос по заказу");

        var result = await controller.Create(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupportTicketDto>(ok.Value);
        Assert.Equal(_order.OrderNumber, dto.OrderNumber);
        Assert.Null(dto.Subject);
    }

    [Fact]
    public async Task Обращение_с_темой_без_заказа_создаётся()
    {
        var controller = MakeController();
        var request = new CreateSupportTicketRequest(OrderId: null, Subject: "Вопрос по доставке", Message: "Когда приедет?");

        var result = await controller.Create(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupportTicketDto>(ok.Value);
        Assert.Null(dto.OrderNumber);
        Assert.Equal("Вопрос по доставке", dto.Subject);
    }

    [Fact]
    public async Task Без_заказа_и_без_темы_отклоняется()
    {
        var controller = MakeController();
        var request = new CreateSupportTicketRequest(OrderId: null, Subject: "   ", Message: "Есть вопрос");

        var result = await controller.Create(request);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
    }

    [Fact]
    public async Task Тема_игнорируется_если_указан_заказ()
    {
        // Заказ выигрывает: тема — запасной вариант для обращений без заказа, а не подпись к нему.
        var controller = MakeController();
        var request = new CreateSupportTicketRequest(_order.Id, Subject: "Побочная тема", Message: "Вопрос");

        var result = await controller.Create(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupportTicketDto>(ok.Value);
        Assert.Equal(_order.OrderNumber, dto.OrderNumber);
        Assert.Null(dto.Subject);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

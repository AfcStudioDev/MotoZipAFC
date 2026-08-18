using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using MotoParts.Api.Controllers;
using MotoParts.Application.Contracts;
using MotoParts.Application.Warehouse;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Регрессия: POST /api/orders падал с 500 при указании адреса доставки —
/// System.Text.Json.JsonException: A possible object cycle was detected
/// (Order.Zip.PartNumber.Zips.PartNumber.Zips...).
///
/// Причина не в самих данных, а в том, что Create() возвращал ActionResult&lt;OrderDto&gt;
/// с телом Ok(order) — сырой сущностью EF. Order.Zip получает fix-up от трекера контекста
/// (Zip загружен в этом же запросе через Include), а Zip.PartNumber.Zips — обратная навигация,
/// которая включает тот же Zip, — отсюда цикл. Тест воспроизводит ровно это состояние
/// трекера (тот же DbContext, тот же Include) и проверяет, что ответ сериализуется.
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

    private OrdersController MakeController(IConfiguration? configuration = null)
    {
        var controller = new OrdersController(_db, new WarehouseService(_db), configuration ?? new ConfigurationBuilder().Build());
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    [Fact]
    public async Task Создание_заказа_с_адресом_доставки_сериализуется_без_ошибок()
    {
        var controller = MakeController();
        var request = new CreateOrderRequest(_zip.Id, 2, 1500m, _address.Id);

        var result = await controller.Create(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OrderDto>(ok.Value);

        // Раньше именно этот вызов бросал JsonException из-за цикла в навигационных
        // свойствах сущности Order — сам факт, что Serialize не бросает, и есть регрессия.
        JsonSerializer.Serialize(dto);

        Assert.Equal("Масляный фильтр", dto.ZipName);
        Assert.Equal("ул. Тестовая, 1", dto.Address);
    }

    [Fact]
    public async Task Заказ_с_несуществующим_адресом_возвращает_404_а_не_падает()
    {
        var controller = MakeController();
        var request = new CreateOrderRequest(_zip.Id, 1, 1500m, AddressId: 999);

        var result = await controller.Create(request);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public void PaymentInfo_возвращает_номер_карты_из_настроек()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Payment:CardNumber"] = "2200 0000 0000 0000" })
            .Build();
        var controller = MakeController(config);

        var result = controller.PaymentInfo();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PaymentInfoDto>(ok.Value);
        Assert.Equal("2200 0000 0000 0000", dto.CardNumber);
    }

    [Fact]
    public void PaymentInfo_без_настройки_возвращает_404_а_не_пустую_карту()
    {
        // Пустая карта в модалке оплаты выглядела бы как баг, а не как «не настроено».
        var controller = MakeController();

        var result = controller.PaymentInfo();

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public async Task Чек_сохраняется_и_имя_файла_проставляется_в_заказ()
    {
        var order = SeedOrder();
        var controller = MakeController();
        var file = MakeFormFile("image/jpeg", "чек.jpg");

        try
        {
            var result = await controller.UploadReceipt(order.Id, file);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<UploadReceiptResponse>(ok.Value);

            var reloaded = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            Assert.Equal(response.ReceiptFileName, reloaded.ReceiptFileName);

            var savedPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Receipts", response.ReceiptFileName);
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            CleanupReceiptFiles();
        }
    }

    [Fact]
    public async Task Чек_недопустимого_типа_отклоняется()
    {
        var order = SeedOrder();
        var controller = MakeController();
        var file = MakeFormFile("text/plain", "чек.txt");

        var result = await controller.UploadReceipt(order.Id, file);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);

        var reloaded = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Null(reloaded.ReceiptFileName);
    }

    [Fact]
    public async Task Чек_для_несуществующего_заказа_возвращает_404()
    {
        var controller = MakeController();
        var file = MakeFormFile("image/jpeg", "чек.jpg");

        var result = await controller.UploadReceipt(Guid.NewGuid(), file);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>Строка заказа для тестов загрузки чека — само создание заказа здесь не проверяется.</summary>
    private Order SeedOrder()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-RECEIPT-TEST",
            CountOrdered = 1,
            ZipId = _zip.Id,
            AddressId = _address.Id,
            UserId = _user.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = 1790m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        };
        _db.Orders.Add(order);
        _db.SaveChanges();
        return order;
    }

    private static IFormFile MakeFormFile(string contentType, string fileName)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static void CleanupReceiptFiles()
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Receipts");
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

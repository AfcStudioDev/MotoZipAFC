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
/// Оформление покупки — из одной позиции или из корзины (несколько позиций одной
/// транзакцией, объединённых в один Purchase). Регрессия на цикл сериализации
/// (Order.Zip.PartNumber.Zips.PartNumber.Zips...) воспроизводится тем же способом,
/// что и раньше для одиночного заказа — см. историю OrdersControllerTests.
/// </summary>
public sealed class PurchasesControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly User _user;
    private readonly DeliveryAddress _address;
    private readonly Zip _zip;
    private readonly Zip _zip2;

    public PurchasesControllerTests()
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
        var partNumber2 = new PartNumber { Id = 2, PartNum = "06455-MEN-000", Name = "Тормозные колодки", GroupId = 1 };
        var donor = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda" };
        _db.ZipGroups.Add(group);
        _db.PartNumbers.Add(partNumber);
        _db.PartNumbers.Add(partNumber2);
        _db.IncomeMotos.Add(donor);

        _user = new User { Email = "buyer@test.local", FIO = "Покупатель" };
        _address = new DeliveryAddress { Address = "ул. Тестовая, 1", User = _user };
        _db.Users.Add(_user);
        _db.DeliveryAddressess.Add(_address);

        _zip = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber.Id, IncomeMotoId = donor.Id, IncomeCost = 1250m, SellCost = 1790m };
        _zip2 = new Zip { Id = Guid.NewGuid(), PartNumId = partNumber2.Id, IncomeMotoId = donor.Id, IncomeCost = 800m, SellCost = 1200m };
        _db.Zips.Add(_zip);
        _db.Zips.Add(_zip2);
        _db.Stored.Add(new Stored { ZipId = _zip.Id, Count = 5 });
        _db.Stored.Add(new Stored { ZipId = _zip2.Id, Count = 5 });

        _db.SaveChanges();
    }

    private PurchasesController MakeController(IConfiguration? configuration = null)
    {
        var controller = new PurchasesController(_db, new WarehouseService(_db), configuration ?? new ConfigurationBuilder().Build());
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };
        return controller;
    }

    [Fact]
    public async Task Покупка_из_одной_позиции_сериализуется_без_ошибок()
    {
        var controller = MakeController();
        var request = new CheckoutRequest([new CartItemRequest(_zip.Id, 2, 1500m)], _address.Id);

        var result = await controller.Checkout(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PurchaseDto>(ok.Value);

        // Раньше именно этот вызов бросал JsonException из-за цикла в навигационных
        // свойствах сущности Order — сам факт, что Serialize не бросает, и есть регрессия.
        JsonSerializer.Serialize(dto);

        var item = Assert.Single(dto.Items);
        Assert.Equal("Масляный фильтр", item.ZipName);
        Assert.Equal("ул. Тестовая, 1", item.Address);
        Assert.Equal(dto.Id, item.PurchaseId);
    }

    [Fact]
    public async Task Корзина_из_нескольких_позиций_создаёт_одну_покупку_и_списывает_каждую_позицию()
    {
        var controller = MakeController();
        var request = new CheckoutRequest(
            [new CartItemRequest(_zip.Id, 2, 1500m), new CartItemRequest(_zip2.Id, 1, 1000m)],
            _address.Id, "СДЕК", "Позвонить заранее");

        var result = await controller.Checkout(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PurchaseDto>(ok.Value);

        Assert.Equal(2, dto.Items.Count);
        // Обе позиции — одна и та же покупка: общий адрес, оплата и доставка.
        Assert.All(dto.Items, item => Assert.Equal(dto.Id, item.PurchaseId));

        var stockZip1 = _db.Stored.AsNoTracking().Single(s => s.ZipId == _zip.Id).Count;
        var stockZip2 = _db.Stored.AsNoTracking().Single(s => s.ZipId == _zip2.Id).Count;
        Assert.Equal(3, stockZip1); // было 5, списали 2
        Assert.Equal(4, stockZip2); // было 5, списали 1

        var purchase = await _db.Purchases.AsNoTracking().SingleAsync(p => p.Id == dto.Id);
        Assert.Equal("СДЕК", purchase.DeliveryCompany);
        Assert.Equal("Позвонить заранее", purchase.DeliveryComment);
        Assert.False(purchase.IsPaid);

        var ordersCount = await _db.Orders.CountAsync(o => o.PurchaseId == dto.Id);
        Assert.Equal(2, ordersCount);
    }

    [Fact]
    public async Task Заказ_с_несуществующим_адресом_возвращает_404_а_не_падает()
    {
        var controller = MakeController();
        var request = new CheckoutRequest([new CartItemRequest(_zip.Id, 1, 1500m)], AddressId: 999);

        var result = await controller.Checkout(request);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Пустая_корзина_отклоняется()
    {
        var controller = MakeController();
        var request = new CheckoutRequest([], _address.Id);

        var result = await controller.Checkout(request);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
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
    public async Task Чек_сохраняется_и_имя_файла_проставляется_в_покупку()
    {
        var purchase = SeedPurchase();
        var controller = MakeController();
        var file = MakeFormFile("image/jpeg", "чек.jpg");

        try
        {
            var result = await controller.UploadReceipt(purchase.Id, file);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<UploadReceiptResponse>(ok.Value);

            var reloaded = await _db.Purchases.AsNoTracking().SingleAsync(p => p.Id == purchase.Id);
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
        var purchase = SeedPurchase();
        var controller = MakeController();
        var file = MakeFormFile("text/plain", "чек.txt");

        var result = await controller.UploadReceipt(purchase.Id, file);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);

        var reloaded = await _db.Purchases.AsNoTracking().SingleAsync(p => p.Id == purchase.Id);
        Assert.Null(reloaded.ReceiptFileName);
    }

    [Fact]
    public async Task Чек_для_несуществующей_покупки_возвращает_404()
    {
        var controller = MakeController();
        var file = MakeFormFile("image/jpeg", "чек.jpg");

        var result = await controller.UploadReceipt(Guid.NewGuid(), file);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>Покупка для тестов загрузки чека — само оформление здесь не проверяется.</summary>
    private Purchase SeedPurchase()
    {
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-RECEIPT-TEST",
            UserId = _user.Id,
            AddressId = _address.Id,
            OrderDateTime = DateTimeOffset.UtcNow
        };
        _db.Purchases.Add(purchase);
        _db.SaveChanges();
        return purchase;
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

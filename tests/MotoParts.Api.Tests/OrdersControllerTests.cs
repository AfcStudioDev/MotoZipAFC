using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    private OrdersController MakeController()
    {
        var controller = new OrdersController(_db, new WarehouseService(_db));
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

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

using MotoParts.Application.Warehouse;
using MotoParts.Domain.Common;
using MotoParts.Domain.Models;

using Xunit;

namespace MotoParts.Application.Tests;

/// <summary>
/// Главный инвариант системы: движение товара меняет остаток и пишет строку журнала — вместе,
/// либо никак. До появления WarehouseService правило было размазано по восьми местам в трёх
/// контроллерах, и каждая из ошибок ниже реально случалась в проде.
/// </summary>
public class WarehouseServiceTests
{
    private static WarehouseService ServiceFor(WarehouseFixture f) => new(f.Db);

    [Fact]
    public async Task Заказ_списывает_остаток_и_пишет_продажу_в_журнал()
    {
        using var f = new WarehouseFixture(initialStock: 10);
        var sut = ServiceFor(f);
        var order = f.AddOrder(qty: 2, orderNumber: "ORD-1");

        var result = await sut.ReserveForOrderAsync(
            f.Zip, qty: 2, order.Id, order.OrderNumber, sellCost: 1500m, userId: null, "Заказ ORD-1");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(8, f.StockOf(f.Zip.Id));

        var log = Assert.Single(f.LogsFor(f.Zip.Id));
        Assert.Equal((short)OperationEnum.Sale, log.OperationId);
        // Продажа записывается отрицательным количеством — от этого зависит отчёт по продажам.
        Assert.Equal(-2, log.Qty);
        Assert.Equal(order.Id, log.OrderId);
        Assert.Equal(1500m, log.SellCost);
    }

    [Fact]
    public async Task Нельзя_заказать_больше_чем_есть_на_складе()
    {
        using var f = new WarehouseFixture(initialStock: 1);
        var sut = ServiceFor(f);

        var result = await sut.ReserveForOrderAsync(
            f.Zip, qty: 5, Guid.NewGuid(), "ORD-2", 1500m, null, "Заказ ORD-2");

        Assert.True(result.IsFailure);
        Assert.Contains("Доступно: 1", result.Error!.Message);
        // Нехватка товара — конфликт с текущим состоянием, а не ошибка в запросе:
        // от этого зависит код ответа (409, а не 400).
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        // Отказ не должен ничего менять: ни остатка, ни записей в журнале.
        Assert.Equal(1, f.StockOf(f.Zip.Id));
        Assert.Empty(f.LogsFor(f.Zip.Id));
    }

    [Fact]
    public async Task Возврат_поднимает_остаток_и_пишет_отдельную_строку_возврата()
    {
        using var f = new WarehouseFixture(initialStock: 8);
        var sut = ServiceFor(f);

        var result = await sut.ReturnFromOrderAsync(
            f.Zip.Id, qty: 2, orderId: null, sellCost: 1500m, userId: null, "Возврат: заказ удалён");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(10, f.StockOf(f.Zip.Id));

        var log = Assert.Single(f.LogsFor(f.Zip.Id));
        Assert.Equal((short)OperationEnum.Refund, log.OperationId);
        Assert.Equal(2, log.Qty);
    }

    [Fact]
    public async Task Удаление_заказа_не_затирает_строку_продажи_а_добавляет_возврат()
    {
        // Регрессия: раньше при удалении заказа журнал по нему удалялся целиком,
        // и в «Истории по детали» продажа подменялась возвратом.
        using var f = new WarehouseFixture(initialStock: 10);
        var sut = ServiceFor(f);
        var order = f.AddOrder(qty: 2, orderNumber: "ORD-3");

        await sut.ReserveForOrderAsync(f.Zip, 2, order.Id, order.OrderNumber, 1500m, null, "Заказ ORD-3");
        await f.Db.SaveChangesAsync();

        await sut.ReturnFromOrderAsync(f.Zip.Id, 2, orderId: null, 1500m, null, "Возврат: заказ ORD-3 удалён");
        await f.Db.SaveChangesAsync();

        var logs = f.LogsFor(f.Zip.Id).OrderBy(l => l.Id).ToList();
        Assert.Equal(2, logs.Count);
        Assert.Equal((short)OperationEnum.Sale, logs[0].OperationId);
        Assert.Equal((short)OperationEnum.Refund, logs[1].OperationId);
        Assert.Equal(10, f.StockOf(f.Zip.Id));
    }

    [Fact]
    public async Task Восстановление_заказа_из_отменённых_списывает_товар_повторно()
    {
        using var f = new WarehouseFixture(initialStock: 10);
        var sut = ServiceFor(f);

        var order = f.AddOrder(qty: 3, orderNumber: "ORD-RESTORE");

        var result = await sut.ReserveAgainAsync(
            f.Zip.Id, qty: 3, order.Id, 1790m, null, "Повторное списание");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(7, f.StockOf(f.Zip.Id));
        Assert.Equal((short)OperationEnum.Sale, Assert.Single(f.LogsFor(f.Zip.Id)).OperationId);
    }

    [Fact]
    public async Task Восстановление_заказа_не_уводит_остаток_в_минус()
    {
        // Товар уже разошёлся — вернуть заказ в работу нельзя, иначе склад покажет минус.
        using var f = new WarehouseFixture(initialStock: 1);
        var sut = ServiceFor(f);

        var result = await sut.ReserveAgainAsync(f.Zip.Id, qty: 3, Guid.NewGuid(), 1790m, null, "Повторное списание");

        Assert.True(result.IsFailure);
        Assert.Equal(1, f.StockOf(f.Zip.Id));
    }

    [Fact]
    public async Task Оприходование_увеличивает_остаток_и_пишет_приход()
    {
        using var f = new WarehouseFixture(initialStock: 0);
        var sut = ServiceFor(f);

        var result = await sut.ReceiveAsync(f.Zip, qty: 5, userId: null, "Оприходование");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(5, f.StockOf(f.Zip.Id));

        var log = Assert.Single(f.LogsFor(f.Zip.Id));
        Assert.Equal((short)OperationEnum.Income, log.OperationId);
        Assert.Equal(5, log.Qty);
        Assert.Equal(f.Zip.IncomeCost, log.UnitCost);
    }

    [Theory]
    [InlineData(5, OperationEnum.Correction)]   // плюс — коррекция в плюс
    [InlineData(-3, OperationEnum.WriteOff)]    // минус — списание
    public async Task Коррекция_выбирает_операцию_по_знаку(int delta, OperationEnum expected)
    {
        using var f = new WarehouseFixture(initialStock: 10);
        var sut = ServiceFor(f);

        var result = await sut.AdjustAsync(f.Zip, delta, userId: null, "инвентаризация");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(10 + delta, f.StockOf(f.Zip.Id));
        Assert.Equal((short)expected, Assert.Single(f.LogsFor(f.Zip.Id)).OperationId);
    }

    [Fact]
    public async Task Коррекция_не_уводит_остаток_в_минус()
    {
        using var f = new WarehouseFixture(initialStock: 2);
        var sut = ServiceFor(f);

        var result = await sut.AdjustAsync(f.Zip, delta: -5, userId: null, "инвентаризация");

        Assert.True(result.IsFailure);
        Assert.Contains("отрицательным", result.Error!.Message);
        Assert.Equal(2, f.StockOf(f.Zip.Id));
        Assert.Empty(f.LogsFor(f.Zip.Id));
    }

    [Fact]
    public async Task Нулевая_коррекция_и_пустая_причина_отклоняются()
    {
        using var f = new WarehouseFixture(initialStock: 5);
        var sut = ServiceFor(f);

        var zeroDelta = await sut.AdjustAsync(f.Zip, 0, null, "причина");
        var emptyComment = await sut.AdjustAsync(f.Zip, 1, null, "   ");

        // Это ошибки во входных данных — 400, а не 409.
        Assert.Equal(ErrorKind.Validation, zeroDelta.Error!.Kind);
        Assert.Equal(ErrorKind.Validation, emptyComment.Error!.Kind);
        Assert.Equal(5, f.StockOf(f.Zip.Id));
    }

    [Fact]
    public async Task Возврат_создаёт_строку_остатка_если_её_не_было()
    {
        using var f = new WarehouseFixture(initialStock: 0);
        // Убираем строку остатка совсем — так бывает у деталей, заведённых в обход прихода.
        f.Db.Stored.RemoveRange(f.Db.Stored.Where(s => s.ZipId == f.Zip.Id));
        await f.Db.SaveChangesAsync();

        var sut = ServiceFor(f);
        var result = await sut.ReturnFromOrderAsync(f.Zip.Id, 2, null, 1790m, null, "Возврат");
        await f.Db.SaveChangesAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, f.StockOf(f.Zip.Id));
    }
}

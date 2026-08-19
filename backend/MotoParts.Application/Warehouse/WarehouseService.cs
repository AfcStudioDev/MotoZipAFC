using Microsoft.EntityFrameworkCore;

using MotoParts.Application.Abstractions;
using MotoParts.Domain.Common;
using MotoParts.Domain.Models;

namespace MotoParts.Application.Warehouse;


/// <summary>
/// Единственный владелец правила «движение товара меняет остаток и пишет строку журнала».
///
/// До этого правило было размазано по восьми местам в трёх контроллерах, и каждое движение
/// товара реализовывалось заново. Отсюда росли ошибки: заказ без записи в журнал, двойной
/// возврат при удалении уже отменённого заказа, уход остатка в минус. Теперь остаток меняется
/// только здесь, а журнал пишется той же операцией — рассинхронизировать их нельзя.
///
/// Транзакцию сервис не открывает и SaveChanges не вызывает: вызывающий код обычно меняет
/// что-то ещё (создаёт заказ, ставит статус), и всё должно попасть в одну транзакцию.
/// </summary>
public class WarehouseService(IAppDbContext db)
{
    /// <summary>Продажа: снимаем количество со склада под конкретный заказ.</summary>
    public async Task<Result<int>> ReserveForOrderAsync(
        Zip zip, int qty, Guid orderId, string orderNumber, decimal sellCost, int? userId, string description)
    {
        if (qty <= 0) return Result<int>.Validation("Количество должно быть больше нуля");

        var stored = await GetStoredAsync(zip.Id);
        if (stored is null || stored.Count < qty)
            return Result<int>.Conflict($"Недостаточно товара на складе. Доступно: {stored?.Count ?? 0}");

        stored.Count -= qty;

        AddLog(OperationEnum.Sale, zip, -qty, userId, description,
            orderId: orderId, unitCost: zip.IncomeCost, sellCost: sellCost);

        return Result<int>.Success(stored.Count);
    }

    /// <summary>
    /// Возврат товара на склад: отмена или удаление заказа. orderId допускает null — при удалении
    /// заказа ссылаться уже не на что, поэтому номер заказа сохраняется в описании.
    /// </summary>
    public async Task<Result<int>> ReturnFromOrderAsync(
        Guid zipId, int qty, Guid? orderId, decimal sellCost, int? userId, string description)
    {
        if (qty <= 0) return Result<int>.Validation("Количество должно быть больше нуля");

        var stored = await GetOrCreateStoredAsync(zipId);
        stored.Count += qty;

        AddLog(OperationEnum.Refund, zip: null, qty: qty, userId: userId, description: description,
            orderId: orderId, zipId: zipId, sellCost: sellCost);

        return Result<int>.Success(stored.Count);
    }

    /// <summary>Повторное списание: заказ вернули из отменённых обратно в работу.</summary>
    public async Task<Result<int>> ReserveAgainAsync(
        Guid zipId, int qty, Guid? orderId, decimal sellCost, int? userId, string description)
    {
        var stored = await GetOrCreateStoredAsync(zipId);

        // Остаток не должен уходить в минус даже здесь: если товар уже разошёлся,
        // возврат заказа в работу — повод разобраться вручную, а не показать отрицательный склад.
        if (stored.Count < qty)
            return Result<int>.Conflict($"Недостаточно товара на складе, чтобы вернуть заказ в работу. Доступно: {stored.Count}");

        stored.Count -= qty;

        AddLog(OperationEnum.Sale, zip: null, qty: -qty, userId: userId, description: description,
            orderId: orderId, zipId: zipId, sellCost: sellCost);

        return Result<int>.Success(stored.Count);
    }

    /// <summary>Оприходование новой партии запчасти.</summary>
    public async Task<Result<int>> ReceiveAsync(Zip zip, int qty, int? userId, string description)
    {
        if (qty < 0) return Result<int>.Validation("Количество не может быть отрицательным");

        var stored = await GetOrCreateStoredAsync(zip.Id);
        stored.Count += qty;

        AddLog(OperationEnum.Income, zip, qty, userId, description,
            unitCost: zip.IncomeCost, sellCost: zip.SellCost);

        return Result<int>.Success(stored.Count);
    }

    /// <summary>Ручная коррекция остатка со знаком: минус — списание, плюс — доприходование.</summary>
    public async Task<Result<int>> AdjustAsync(Zip zip, int delta, int? userId, string comment)
    {
        if (delta == 0) return Result<int>.Validation("Изменение количества не может быть нулевым");
        if (string.IsNullOrWhiteSpace(comment)) return Result<int>.Validation("Причина коррекции обязательна");

        var stored = await GetOrCreateStoredAsync(zip.Id);
        if (stored.Count + delta < 0)
            return Result<int>.Conflict($"Остаток не может стать отрицательным. Сейчас на складе: {stored.Count}");

        stored.Count += delta;

        AddLog(delta < 0 ? OperationEnum.WriteOff : OperationEnum.Correction,
            zip, delta, userId, comment.Trim(), unitCost: zip.IncomeCost);

        return Result<int>.Success(stored.Count);
    }

    private async Task<Stored?> GetStoredAsync(Guid zipId) =>
        await db.Stored.FirstOrDefaultAsync(s => s.ZipId == zipId);

    private async Task<Stored> GetOrCreateStoredAsync(Guid zipId)
    {
        var stored = await GetStoredAsync(zipId);
        if (stored is null)
        {
            stored = new Stored { ZipId = zipId, Count = 0 };
            db.Stored.Add(stored);
        }
        return stored;
    }

    private void AddLog(
        OperationEnum operation, Zip? zip, int qty, int? userId, string description,
        Guid? orderId = null, Guid? zipId = null, decimal? unitCost = null, decimal? sellCost = null)
    {
        db.Logs.Add(new Log
        {
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = (short)operation,
            OrderId = orderId,
            ZipId = zip?.Id ?? zipId,
            UserId = userId,
            Qty = qty,
            UnitCost = unitCost,
            SellCost = sellCost,
            Description = description
        });
    }
}

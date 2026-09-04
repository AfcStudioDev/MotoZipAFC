using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Infrastructure.Persistence;
using MotoParts.Domain.Models;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Отчёты по складу и продажам. Вынесены из AdminController в отдельный контроллер,
/// потому что нужны Sender-у наравне с Admin и Registrar — а [Authorize] класса
/// и метода комбинируются через И (оба должны пройти), а не через ИЛИ, так что
/// метод-уровневый Authorize с более широким списком ролей класс-уровневый
/// не расширяет и не заменяет. Единственный способ выдать Sender доступ только
/// к отчётам, не открывая ему остальные методы AdminController, — свой контроллер.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin,Registrar,Sender")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Переводит границы периода в UTC.
    /// Принимаем именно DateOnly: при биндинге "2026-05-01" в DateTimeOffset подставляется
    /// смещение сервера, а Npgsql пишет в timestamptz только значения с нулевым смещением
    /// и падает с ArgumentException.
    /// Конец периода сдвигается на сутки вперёд, чтобы указанный день входил в отчёт целиком.
    /// </summary>
    private static (DateTimeOffset? From, DateTimeOffset? To) PeriodToUtc(DateOnly? from, DateOnly? to)
    {
        DateTimeOffset? fromUtc = from.HasValue
            ? new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;

        DateTimeOffset? toUtc = to.HasValue
            ? new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;

        return (fromUtc, toUtc);
    }

    /// <summary>
    /// Фильтр по детали, общий для отчётов, которые строятся по журналу движений.
    /// ILike, а не ToLower().Contains(): наименования русские, и регистронезависимость
    /// в Postgres даёт именно он (так же сделан поиск в каталоге).
    /// </summary>
    private static IQueryable<Log> FilterByZip(IQueryable<Log> query, string? partNum, string? name, string? donor)
    {
        if (!string.IsNullOrWhiteSpace(partNum))
            query = query.Where(l => EF.Functions.ILike(l.Zip!.PartNumber.PartNum, $"%{partNum.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(l => EF.Functions.ILike(l.Zip!.PartNumber.Name, $"%{name.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(donor))
            query = query.Where(l => EF.Functions.ILike(l.Zip!.IncomeMoto.Description, $"%{donor.Trim()}%"));

        return query;
    }

    /// <summary>
    /// Минимальный список деталей для выпадающих списков на странице отчётов.
    /// Отдельно от GET /admin/zip: тот эндпоинт остаётся закрыт для Sender (несёт закупочные
    /// цены и остатки), а здесь — только то, что нужно для выбора детали в фильтре отчёта.
    /// Донор идёт тем же списком: он нужен для подсказок в фильтрах, и отдельный запрос
    /// ради одного поля страница делать не должна.
    /// </summary>
    [HttpGet("zip-lookup")]
    public async Task<IActionResult> ZipLookup() =>
        Ok(await db.Zips
            .OrderBy(z => z.PartNumber.Name)
            .Select(z => new
            {
                z.Id,
                Name = z.PartNumber.Name,
                PartNum = z.PartNumber.PartNum,
                IncomeMoto = z.IncomeMoto.Description
            })
            .ToListAsync());

    /// <summary>
    /// Остатки склада: всё, чего физически больше нуля. Без периода — это срез на сейчас.
    /// «Стоимость» здесь — цена продажи, а не закупочная: отчёт открыт и для Sender,
    /// которому закупочные цены не показываем (по той же причине, что и в zip-lookup).
    /// </summary>
    [HttpGet("stock")]
    public async Task<IActionResult> StockReport([FromQuery] string? donor)
    {
        var query = db.Stored.Where(s => s.Count > 0);

        if (!string.IsNullOrWhiteSpace(donor))
            query = query.Where(s => EF.Functions.ILike(s.Zip.IncomeMoto.Description, $"%{donor.Trim()}%"));

        return Ok(await query
            .OrderBy(s => s.Zip.PartNumber.Name)
            .Select(s => new
            {
                s.ZipId,
                Name = s.Zip.PartNumber.Name,
                PartNum = s.Zip.PartNumber.PartNum,
                s.Count,
                SellCost = s.Zip.SellCost,
                Total = (s.Zip.SellCost ?? 0m) * s.Count,
                IncomeMoto = s.Zip.IncomeMoto.Description
            })
            .ToListAsync());
    }

    /// <summary>Отчёт по проданным деталям за период. Границы включают обе указанные даты.</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> SalesReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? partNum,
        [FromQuery] string? name,
        [FromQuery] string? donor)
    {
        var (fromUtc, toUtc) = PeriodToUtc(from, to);

        var query = db.Logs.Where(l => l.OperationId == (short)OperationEnum.Sale && l.ZipId != null);
        if (fromUtc.HasValue) query = query.Where(l => l.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(l => l.CreatedAt < toUtc.Value);
        query = FilterByZip(query, partNum, name, donor);

        var rows = await query
            // Донор входит в ключ группировки, а не берётся отдельно: у Zip он один,
            // так что строк это не дробит, но позволяет вывести его в колонке.
            .GroupBy(l => new
            {
                l.ZipId,
                Name = l.Zip!.PartNumber.Name,
                PartNum = l.Zip!.PartNumber.PartNum,
                IncomeMoto = l.Zip!.IncomeMoto.Description
            })
            .Select(g => new
            {
                ZipId = g.Key.ZipId,
                g.Key.Name,
                g.Key.PartNum,
                g.Key.IncomeMoto,
                // Qty у продажи отрицательный, поэтому меняем знак.
                Sold = -g.Sum(l => l.Qty ?? 0),
                Revenue = g.Sum(l => -(l.Qty ?? 0) * (l.SellCost ?? 0m)),
                Cost = g.Sum(l => -(l.Qty ?? 0) * (l.UnitCost ?? 0m))
            })
            .OrderByDescending(r => r.Revenue)
            .ToListAsync();

        return Ok(rows.Select(r => new { r.ZipId, r.Name, r.PartNum, r.IncomeMoto, r.Sold, r.Revenue, r.Cost, Margin = r.Revenue - r.Cost }));
    }

    /// <summary>Отчёт по поступившим деталям за период. Границы включают обе указанные даты.</summary>
    [HttpGet("income")]
    public async Task<IActionResult> IncomeReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? partNum,
        [FromQuery] string? name,
        [FromQuery] string? donor)
    {
        var (fromUtc, toUtc) = PeriodToUtc(from, to);

        var query = db.Logs.Where(l => l.OperationId == (short)OperationEnum.Income && l.ZipId != null);
        if (fromUtc.HasValue) query = query.Where(l => l.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(l => l.CreatedAt < toUtc.Value);
        query = FilterByZip(query, partNum, name, donor);

        return Ok(await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.CreatedAt,
                l.ZipId,
                Name = l.Zip!.PartNumber.Name,
                PartNum = l.Zip!.PartNumber.PartNum,
                Qty = l.Qty ?? 0,
                l.UnitCost,
                Total = (l.Qty ?? 0) * (l.UnitCost ?? 0m),
                IncomeMoto = l.Zip!.IncomeMoto.Description
            })
            .ToListAsync());
    }

    /// <summary>История наценки и уценки. Без zipId — по всем деталям.</summary>
    [HttpGet("price-history")]
    public async Task<IActionResult> PriceHistoryReport([FromQuery] Guid? zipId)
    {
        var query = db.PriceHistories.AsQueryable();
        if (zipId.HasValue) query = query.Where(p => p.ZipId == zipId.Value);

        return Ok(await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.CreatedAt,
                p.ZipId,
                Name = p.Zip.PartNumber.Name,
                PartNum = p.Zip.PartNumber.PartNum,
                p.OldCost,
                p.NewCost,
                Delta = p.NewCost - p.OldCost,
                Operation = p.Operation.Description,
                User = p.User.FIO,
                p.Comment
            })
            .ToListAsync());
    }

    /// <summary>Полная лента движений по конкретной детали.</summary>
    [HttpGet("zip-history/{zipId:guid}")]
    public async Task<IActionResult> ZipHistory(Guid zipId) =>
        Ok(await db.Logs.Where(l => l.ZipId == zipId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.CreatedAt,
                Operation = l.Operation.Description,
                l.Qty,
                l.UnitCost,
                l.SellCost,
                l.OrderId,
                User = l.User != null ? l.User.FIO : null,
                l.Description
            })
            .ToListAsync());
}

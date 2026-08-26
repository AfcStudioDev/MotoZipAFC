using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
using MotoParts.Application.Contracts;
using MotoParts.Application.Warehouse;
using MotoParts.Domain.Common;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Коррекция остатка и переоценка. Вынесены из AdminWarehouseController в отдельный контроллер
/// по той же причине, что и ReportsController: [Authorize] класса и метода комбинируются через
/// И, а не через ИЛИ, так что метод-уровневый Authorize с более широким списком ролей
/// класс-уровневый (Admin,Registrar в AdminControllerBase) не расширяет. Единственный способ
/// выдать Sender доступ только к этим двум действиям, не открывая ему остальной склад
/// (AddZip/UpdateZip/фото и т.д.), — свой контроллер вне этой цепочки наследования.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Sender")]
public class WarehouseCorrectionsController(AppDbContext db, WarehouseService warehouse) : ControllerBase
{
    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Ручная коррекция остатка со знаком: +5 доприходовать, −3 списать.</summary>
    [HttpPost("corrections")]
    public async Task<IActionResult> AddCorrection(AdminCorrectionRequest request)
    {
        if (request.Delta == 0)
            return BadRequest(new { message = "Изменение количества не может быть нулевым" });
        if (string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new { message = "Причина коррекции обязательна" });

        var zip = await db.Zips.Include(z => z.PartNumber).FirstOrDefaultAsync(z => z.Id == request.ZipId);
        if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

        await using var tx = await db.Database.BeginTransactionAsync();

        // Знак дельты сам определяет операцию: минус — списание, плюс — коррекция в плюс.
        var stock = await warehouse.AdjustAsync(zip, request.Delta, CurrentUserId, request.Comment);
        if (stock.IsFailure) return stock.Error!.ToErrorResponse();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "Коррекция проведена", zipId = zip.Id, count = stock.Value });
    }

    /// <summary>Изменение цены продажи с записью в историю переоценки.</summary>
    [HttpPost("reprice")]
    public async Task<IActionResult> Reprice(AdminRepriceRequest request)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });

        var zip = await db.Zips.FindAsync(request.ZipId);
        if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

        var oldCost = zip.SellCost ?? 0m;
        if (oldCost == request.NewCost)
            return BadRequest(new { message = "Новая цена совпадает с текущей" });

        zip.SellCost = request.NewCost;

        db.PriceHistories.Add(new PriceHistory
        {
            ZipId = zip.Id,
            OldCost = oldCost,
            NewCost = request.NewCost,
            OperationId = (short)(request.NewCost > oldCost ? OperationEnum.Markup : OperationEnum.Markdown),
            UserId = userId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            Comment = request.Comment
        });

        await db.SaveChangesAsync();
        return Ok(new { message = "Цена обновлена", oldCost, newCost = request.NewCost });
    }
}

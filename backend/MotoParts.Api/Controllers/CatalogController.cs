using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Поиск запчастей: строка запроса + фильтры (марка, модель, группа ZIP, год выпуска, парт-номер).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ZipDto>>> Search(
        [FromQuery] string? query,
        [FromQuery] int? markId,
        [FromQuery] int? modelId,
        [FromQuery] int? groupId,
        [FromQuery] int? year,
        [FromQuery] string? partNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Классификация живёт на каталожной позиции, применимость к моделям — в PartNumberApplicability.
        var zips = db.Zips
            .Include(z => z.PartNumber).ThenInclude(p => p.Group)
            .Include(z => z.PartNumber).ThenInclude(p => p.Applicability).ThenInclude(a => a.Model).ThenInclude(m => m.Mark)
            .Include(z => z.Photos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            zips = zips.Where(z =>
                EF.Functions.ILike(z.PartNumber.Name, $"%{q}%") ||
                EF.Functions.ILike(z.PartNumber.PartNum, $"%{q}%") ||
                z.PartNumber.Applicability.Any(a =>
                    EF.Functions.ILike(a.Model.Model, $"%{q}%") ||
                    (a.Model.Mark != null && EF.Functions.ILike(a.Model.Mark.Mark, $"%{q}%"))));
        }

        // Деталь подходит к нескольким моделям, поэтому фильтры идут через Any — дублей строк не возникает.
        if (markId.HasValue)
            zips = zips.Where(z => z.PartNumber.Applicability.Any(a => a.Model.MarkId == markId));
        if (modelId.HasValue)
            zips = zips.Where(z => z.PartNumber.Applicability.Any(a => a.ModelId == modelId));
        if (groupId.HasValue) zips = zips.Where(z => z.PartNumber.GroupId == groupId);
        if (year.HasValue) zips = zips.Where(z => z.Year != null && z.Year.Value == year);
        if (!string.IsNullOrWhiteSpace(partNumber))
            zips = zips.Where(z => EF.Functions.ILike(z.PartNumber.PartNum, $"%{partNumber.Trim()}%"));

        var total = await zips.CountAsync();
        var items = await zips
            .OrderBy(z => z.PartNumber.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(z => new ZipDto(
                z.Id,
                z.PartNumber.Name,
                z.IncomeCost,
                z.SellCost,
                z.PartNumber.PartNum,
                z.PartNumber.Applicability
                    .Where(a => a.Model.Mark != null)
                    .Select(a => a.Model.Mark!.Mark)
                    .Distinct()
                    .ToList(),
                z.PartNumber.Applicability.Select(a => a.Model.Model).Distinct().ToList(),
                z.PartNumber.Group != null ? z.PartNumber.Group.GroupName : null,
                z.Year,
                z.IncomeMotoId,
                z.Stored != null ? z.Stored.Count : 0,
                z.Photos.Select(p => p.FileName).ToList(),
                z.Comment
            ))
            .ToListAsync();

        return Ok(new PagedResult<ZipDto>(items, total, page, pageSize));
    }

    [HttpGet("marks")]
    public async Task<IActionResult> Marks() =>
        Ok(await db.MotoMarks.OrderBy(m => m.Mark).Select(m => new { m.Id, m.Mark }).ToListAsync());

    [HttpGet("models")]
    public async Task<IActionResult> Models([FromQuery] int? markId)
    {
        var models = db.MotoModels.AsQueryable();
        if (markId.HasValue) models = models.Where(m => m.MarkId == markId);
        return Ok(await models.OrderBy(m => m.Model).Select(m => new { m.Id, m.MarkId, m.Model }).ToListAsync());
    }

    [HttpGet("groups")]
    public async Task<IActionResult> Groups() =>
        Ok(await db.ZipGroups.OrderBy(g => g.GroupName).Select(g => new { g.Id, g.GroupName }).ToListAsync());

    [HttpGet("years")]
    public async Task<IActionResult> Years() =>
        Ok(await db.Zips.Where(z => z.Year != null)
            .Select(z => z.Year!.Value).Distinct().OrderByDescending(y => y).ToListAsync());

    /// <summary>
    /// Подсказки для поля «Part number» в фильтрах каталога.
    /// Отдаются только парт-номера, по которым реально заведены запчасти —
    /// иначе подсказка приводила бы к пустой выдаче.
    /// </summary>
    [HttpGet("part-numbers")]
    public async Task<IActionResult> PartNumberSuggestions([FromQuery] string? query, [FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        var partNumbers = db.PartNumbers.Where(p => p.Zips.Any());

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            partNumbers = partNumbers.Where(p => EF.Functions.ILike(p.PartNum, $"%{q}%"));
        }

        return Ok(await partNumbers
            .OrderBy(p => p.PartNum)
            .Take(limit)
            .Select(p => new { p.PartNum, p.Name })
            .ToListAsync());
    }
}

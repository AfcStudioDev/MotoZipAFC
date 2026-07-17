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

        var zips = db.Zip
            .Include(z => z.Mark)
            .Include(z => z.Model)
            .Include(z => z.Group)
            .Include(z => z.PartNumber)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            zips = zips.Where(z =>
                EF.Functions.ILike(z.Name, $"%{q}%") ||
                (z.PartNumber != null && EF.Functions.ILike(z.PartNumber.Number, $"%{q}%")) ||
                (z.Mark != null && EF.Functions.ILike(z.Mark.Mark, $"%{q}%")) ||
                (z.Model != null && EF.Functions.ILike(z.Model.Model, $"%{q}%")));
        }

        if (markId.HasValue) zips = zips.Where(z => z.MarkId == markId);
        if (modelId.HasValue) zips = zips.Where(z => z.ModelId == modelId);
        if (groupId.HasValue) zips = zips.Where(z => z.GroupId == groupId);
        if (year.HasValue) zips = zips.Where(z => z.Year != null && z.Year.Value.Year == year);
        if (!string.IsNullOrWhiteSpace(partNumber))
            zips = zips.Where(z => z.PartNumber != null && EF.Functions.ILike(z.PartNumber.Number, $"%{partNumber.Trim()}%"));

        var total = await zips.CountAsync();
        var items = await zips
            .OrderBy(z => z.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(z => new ZipDto(
                z.Id, z.Name, z.Cost, z.CountStored,
                z.PartNumber != null ? z.PartNumber.Number : null,
                z.Mark != null ? z.Mark.Mark : null,
                z.Model != null ? z.Model.Model : null,
                z.Group != null ? z.Group.GroupName : null,
                z.Year != null ? z.Year.Value.Year : null))
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
        Ok(await db.Zip.Where(z => z.Year != null)
            .Select(z => z.Year!.Value.Year).Distinct().OrderByDescending(y => y).ToListAsync());
}

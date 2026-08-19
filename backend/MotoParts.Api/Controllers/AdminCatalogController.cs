using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Application.Contracts;
using MotoParts.Application.Warehouse;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Infrastructure.Services;

using PdfGeneration.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace MotoParts.Api.Controllers;

/// <summary>Каталог: марки, серии, модели, группы, парт-номера и применимость.</summary>
public class AdminCatalogController(AppDbContext db) : AdminControllerBase
{
    // ---------- MotoMarks ----------
    [HttpGet("marks")]
    public async Task<IActionResult> Marks() =>
        Ok(await db.MotoMarks.OrderBy(m => m.Id).Select(m => new { m.Id, m.Mark }).ToListAsync());

    [HttpPost("marks")]
    public async Task<IActionResult> AddMark(AdminMarkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mark))
            return BadRequest(new { message = "Название марки обязательно" });
        if (await db.MotoMarks.AnyAsync(m => m.Mark == request.Mark.Trim()))
            return Conflict(new { message = "Такая марка уже существует" });

        var mark = new MotoMark { Mark = request.Mark.Trim() };
        db.MotoMarks.Add(mark);
        await db.SaveChangesAsync();
        return Ok(new { mark.Id, mark.Mark });
    }

    // ---------- MotoSeries ----------
    [HttpGet("series")]
    public async Task<IActionResult> Series() =>
        Ok(await db.MotoSeries.OrderBy(s => s.SeriesName)
            .Select(s => new { s.Id, s.SeriesName })
            .ToListAsync());

    [HttpPost("series")]
    public async Task<IActionResult> AddSeries(AdminSeriesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SeriesName))
            return BadRequest(new { message = "Название серии обязательно" });

        var series = new MotoSeries { Id = Guid.NewGuid(), SeriesName = request.SeriesName.Trim() };
        db.MotoSeries.Add(series);
        await db.SaveChangesAsync();
        return Ok(new { series.Id, series.SeriesName });
    }

    [AdminOnly]
    [HttpPut("series/{id:guid}")]
    public async Task<IActionResult> UpdateSeries(Guid id, AdminSeriesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SeriesName))
            return BadRequest(new { message = "Название серии обязательно" });

        var series = await db.MotoSeries.FindAsync(id);
        if (series == null) return NotFound(new { message = "Серия не найдена" });

        series.SeriesName = request.SeriesName.Trim();
        await db.SaveChangesAsync();
        return Ok(new { series.Id, series.SeriesName });
    }

    // ---------- MotoModels ----------
    [HttpGet("models")]
    public async Task<IActionResult> Models() =>
        Ok(await db.MotoModels.OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.MarkId, m.Model, Mark = m.Mark != null ? m.Mark.Mark : null })
            .ToListAsync());

    [HttpPost("models")]
    public async Task<IActionResult> AddModel(AdminModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model)) 
            return BadRequest(new { message = "Название модели обязательно" });

        var model = new MotoModel { MarkId = request.MarkId, Model = request.Model.Trim() };
        db.MotoModels.Add(model);
        await db.SaveChangesAsync();
        return Ok(new { model.Id, model.MarkId, model.Model });
    }

    // ---------- ZipGroups ----------
    [HttpGet("groups")]
    public async Task<IActionResult> Groups() =>
        Ok(await db.ZipGroups.OrderBy(g => g.Id).Select(g => new { g.Id, g.GroupName }).ToListAsync());

    [HttpPost("groups")]
    public async Task<IActionResult> AddGroup(AdminGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest(new { message = "Название группы обязательно" });

        var group = new ZipGroup { GroupName = request.GroupName.Trim() };
        db.ZipGroups.Add(group);
        await db.SaveChangesAsync();
        return Ok(new { group.Id, group.GroupName });
    }

    // ---------- PartNumbers ----------
    [HttpGet("part-numbers")]
    public async Task<IActionResult> PartNumbers() =>
        Ok(await db.PartNumbers.OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.PartNum,
                p.Name,
                p.GroupId,
                Group = p.Group != null ? p.Group.GroupName : null
            })
            .ToListAsync());

    [HttpPost("part-numbers")]
    public async Task<IActionResult> AddPartNumber(AdminPartNumberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PartNum))
            return BadRequest(new { message = "Номер запчасти обязателен" });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Наименование обязательно" });
        if (await db.PartNumbers.AnyAsync(p => p.PartNum == request.PartNum.Trim()))
            return Conflict(new { message = "Такой парт-номер уже существует" });

        var pn = new PartNumber
        {
            PartNum = request.PartNum.Trim(),
            Name = request.Name.Trim(),
            GroupId = request.GroupId
        };
        db.PartNumbers.Add(pn);
        await db.SaveChangesAsync();
        return Ok(new { pn.Id, pn.PartNum, pn.Name, pn.GroupId });
    }

    /// <summary>
    /// Правка каталожной позиции. Нужна отдельным методом: обобщённый PUT {table}/{id}
    /// ищет таблицу по имени и на "part-numbers" с дефисом не срабатывает.
    /// </summary>
    [AdminOnly]
    [HttpPut("part-numbers/{id:int}")]
    public async Task<IActionResult> UpdatePartNumber(int id, [FromBody] AdminPartNumberRequest request)
    {
        var pn = await db.PartNumbers.FindAsync(id);
        if (pn == null) return NotFound(new { message = "Парт-номер не найден" });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Наименование обязательно" });

        if (!string.IsNullOrWhiteSpace(request.PartNum))
        {
            var partNum = request.PartNum.Trim();
            if (partNum != pn.PartNum && await db.PartNumbers.AnyAsync(p => p.PartNum == partNum))
                return Conflict(new { message = "Такой парт-номер уже существует" });
            pn.PartNum = partNum;
        }

        pn.Name = request.Name.Trim();
        pn.GroupId = request.GroupId;

        await db.SaveChangesAsync();
        return Ok(new { pn.Id, pn.PartNum, pn.Name, pn.GroupId });
    }

    // ---------- PartNumberApplicability ----------
    [HttpGet("applicability")]
    public async Task<IActionResult> Applicability() =>
        Ok(await db.PartNumberApplicabilities.OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.PartNumId,
                PartNum = a.PartNumber.PartNum,
                Name = a.PartNumber.Name,
                a.ModelId,
                Model = a.Model.Model,
                Mark = a.Model.Mark != null ? a.Model.Mark.Mark : null
            })
            .ToListAsync());

    [HttpPost("applicability")]
    public async Task<IActionResult> AddApplicability(AdminApplicabilityRequest request)
    {
        if (!await db.PartNumbers.AnyAsync(p => p.Id == request.PartNumId))
            return BadRequest(new { message = "Парт-номер не найден" });
        if (!await db.MotoModels.AnyAsync(m => m.Id == request.ModelId))
            return BadRequest(new { message = "Модель не найдена" });
        if (await db.PartNumberApplicabilities.AnyAsync(a => a.PartNumId == request.PartNumId && a.ModelId == request.ModelId))
            return Conflict(new { message = "Такая привязка уже существует" });

        var link = new PartNumberApplicability { PartNumId = request.PartNumId, ModelId = request.ModelId };
        db.PartNumberApplicabilities.Add(link);
        await db.SaveChangesAsync();
        return Ok(new { link.Id, link.PartNumId, link.ModelId });
    }

    // ---------- PartNumberSeriesApplicability ----------
    // Независимая от моделей привязка: у одного парт-номера может быть
    // любое число моделей и любое число серий одновременно.
    [HttpGet("series-applicability")]
    public async Task<IActionResult> SeriesApplicability() =>
        Ok(await db.PartNumberSeriesApplicabilities.OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.PartNumId,
                PartNum = a.PartNumber.PartNum,
                Name = a.PartNumber.Name,
                a.SeriesId,
                Series = a.Series.SeriesName
            })
            .ToListAsync());

    [HttpPost("series-applicability")]
    public async Task<IActionResult> AddSeriesApplicability(AdminSeriesApplicabilityRequest request)
    {
        if (!await db.PartNumbers.AnyAsync(p => p.Id == request.PartNumId))
            return BadRequest(new { message = "Парт-номер не найден" });
        if (!await db.MotoSeries.AnyAsync(s => s.Id == request.SeriesId))
            return BadRequest(new { message = "Серия не найдена" });
        if (await db.PartNumberSeriesApplicabilities.AnyAsync(a => a.PartNumId == request.PartNumId && a.SeriesId == request.SeriesId))
            return Conflict(new { message = "Такая привязка уже существует" });

        var link = new PartNumberSeriesApplicability { PartNumId = request.PartNumId, SeriesId = request.SeriesId };
        db.PartNumberSeriesApplicabilities.Add(link);
        await db.SaveChangesAsync();
        return Ok(new { link.Id, link.PartNumId, link.SeriesId });
    }

    // ---------- Точечные обновления справочников ----------
    //
    // Раньше здесь был обобщённый PUT {table}/{id}, который через рефлексию писал любое
    // свойство любой сущности из присланного JSON. Это давало mass assignment: роль Registrar,
    // имеющая доступ к этому контроллеру, могла выставить себе IsAdmin, переписать PasswordHash
    // или поправить остаток в Stored в обход журнала операций. Ниже — по одному методу на
    // ресурс, каждый принимает свой DTO, поэтому набор изменяемых полей задан явно.

    [AdminOnly]
    [HttpPut("marks/{id:int}")]
    public async Task<IActionResult> UpdateMark(int id, AdminMarkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mark))
            return BadRequest(new { message = "Название марки обязательно" });

        var mark = await db.MotoMarks.FindAsync(id);
        if (mark == null) return NotFound(new { message = "Марка не найдена" });

        var name = request.Mark.Trim();
        if (name != mark.Mark && await db.MotoMarks.AnyAsync(m => m.Mark == name))
            return Conflict(new { message = "Такая марка уже существует" });

        mark.Mark = name;
        await db.SaveChangesAsync();
        return Ok(new { mark.Id, mark.Mark });
    }

    [AdminOnly]
    [HttpPut("models/{id:int}")]
    public async Task<IActionResult> UpdateModel(int id, AdminModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { message = "Название модели обязательно" });

        var model = await db.MotoModels.FindAsync(id);
        if (model == null) return NotFound(new { message = "Модель не найдена" });

        if (!await db.MotoMarks.AnyAsync(m => m.Id == request.MarkId))
            return BadRequest(new { message = "Марка не найдена" });

        model.MarkId = request.MarkId;
        model.Model = request.Model.Trim();
        await db.SaveChangesAsync();
        return Ok(new { model.Id, model.MarkId, model.Model });
    }

    [AdminOnly]
    [HttpPut("groups/{id:int}")]
    public async Task<IActionResult> UpdateGroup(int id, AdminGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest(new { message = "Название группы обязательно" });

        var group = await db.ZipGroups.FindAsync(id);
        if (group == null) return NotFound(new { message = "Группа не найдена" });

        group.GroupName = request.GroupName.Trim();
        await db.SaveChangesAsync();
        return Ok(new { group.Id, group.GroupName });
    }

    [AdminOnly]
    [HttpPut("applicability/{id:int}")]
    public async Task<IActionResult> UpdateApplicability(int id, AdminApplicabilityRequest request)
    {
        var link = await db.PartNumberApplicabilities.FindAsync(id);
        if (link == null) return NotFound(new { message = "Связь не найдена" });

        if (!await db.PartNumbers.AnyAsync(p => p.Id == request.PartNumId))
            return BadRequest(new { message = "Парт-номер не найден" });
        if (!await db.MotoModels.AnyAsync(m => m.Id == request.ModelId))
            return BadRequest(new { message = "Модель не найдена" });

        link.PartNumId = request.PartNumId;
        link.ModelId = request.ModelId;
        await db.SaveChangesAsync();
        return Ok(new { link.Id, link.PartNumId, link.ModelId });
    }

    [AdminOnly]
    [HttpPut("series-applicability/{id:int}")]
    public async Task<IActionResult> UpdateSeriesApplicability(int id, AdminSeriesApplicabilityRequest request)
    {
        var link = await db.PartNumberSeriesApplicabilities.FindAsync(id);
        if (link == null) return NotFound(new { message = "Связь не найдена" });

        if (!await db.PartNumbers.AnyAsync(p => p.Id == request.PartNumId))
            return BadRequest(new { message = "Парт-номер не найден" });
        if (!await db.MotoSeries.AnyAsync(s => s.Id == request.SeriesId))
            return BadRequest(new { message = "Серия не найдена" });

        link.PartNumId = request.PartNumId;
        link.SeriesId = request.SeriesId;
        await db.SaveChangesAsync();
        return Ok(new { link.Id, link.PartNumId, link.SeriesId });
    }

}

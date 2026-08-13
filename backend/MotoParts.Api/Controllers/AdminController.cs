using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;
using MotoParts.Api.Services;
using PdfGeneration.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

using System.Security.Claims;

namespace MotoParts.Api.Controllers;

/// <summary>Админ-панель: ручное добавление записей в каждую таблицу и просмотр содержимого.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin   ,Registrar")]
public class AdminController(AppDbContext db) : ControllerBase
{
    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

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

    // ---------- Zip ----------
    [HttpGet("zip")]
    public async Task<IActionResult> Zips() =>
        Ok(await db.Zips.OrderBy(z => z.PartNumber.Name)
            .Select(z => new
            {
                z.Id,
                Name = z.PartNumber.Name,
                z.IncomeCost,
                z.SellCost,
                z.PartNumId,
                PartNum = z.PartNumber.PartNum,
                GroupId = z.PartNumber.GroupId,
                Group = z.PartNumber.Group != null ? z.PartNumber.Group.GroupName : null,
                Models = z.PartNumber.Applicability.Select(a => a.Model.Model).ToList(),
                Marks = z.PartNumber.Applicability
                    .Where(a => a.Model.Mark != null)
                    .Select(a => a.Model.Mark!.Mark)
                    .Distinct()
                    .ToList(),
                z.Year,
                z.IncomeDate,
                z.Comment,
                z.IncomeMotoId,
                IncomeMoto = z.IncomeMoto != null ? z.IncomeMoto.Description : null,
                CountStored = z.Stored != null ? z.Stored.Count : 0,
                Photos = z.Photos.Select(p => new { p.Id, p.FileName, p.IsMain }).ToList()
            })
            .ToListAsync());

    [HttpPost("zip")]
    public async Task<IActionResult> AddZip([FromForm] AdminZipRequest request)
    {
        var partNumber = await db.PartNumbers.FindAsync(request.PartNumId);
        if (partNumber == null)
            return BadRequest(new { message = "Парт-номер не найден" });
        if (!await db.IncomeMotos.AnyAsync(i => i.Id == request.IncomeMotoId))
            return BadRequest(new { message = "Донор не найден" });
        if (request.CountStored < 0)
            return BadRequest(new { message = "Количество не может быть отрицательным" });

        var zip = new Zip
        {
            Id = Guid.NewGuid(),
            IncomeCost = request.IncomeCost,
            SellCost = request.SellCost,
            PartNumId = request.PartNumId,
            Year = request.Year,
            IncomeMotoId = request.IncomeMotoId,
            IncomeDate = request.IncomeDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Comment = request.Comment
        };

        db.Zips.Add(zip);

        // Остаток и приходное движение создаются вместе с деталью.
        db.Stored.Add(new Stored { ZipId = zip.Id, Count = request.CountStored });
        db.Logs.Add(new Log
        {
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = (short)OperationEnum.Income,
            ZipId = zip.Id,
            UserId = CurrentUserId,
            Qty = request.CountStored,
            UnitCost = request.IncomeCost,
            SellCost = request.SellCost,
            Description = $"Оприходование: {partNumber.Name} ({partNumber.PartNum})"
        });

        await db.SaveChangesAsync();
        var photos = Request.Form.Files;
        // 2. Обработка фотографий
        if (photos != null && photos.Count > 0)
        {
            if (photos.Count > 3)
            {
                return BadRequest(new { message = "Разрешено загружать не более 3-х фотографий." });
            }

            // Указываем путь к папке ZipPhotos (например, в wwwroot)
            string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ZipPhotos");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                if (photo.Length == 0) continue;

                string uniqueFileName = await SavePhotoAsWebpAsync(photo, uploadFolder, zip.Id, i);

                db.ZipPhotos.Add(new ZipPhoto
                {
                    ZipId = zip.Id,
                    FileName = uniqueFileName,
                    IsMain = (i == 0) // Первое фото делаем главным
                });
            }
            await db.SaveChangesAsync();
        }

        return Ok(new { zip.Id, Name = partNumber.Name });
    }

    [HttpPut("zip/{id}")]
    public async Task<IActionResult> UpdateZip(Guid id, [FromForm] AdminZipRequest request)
    {
        // 1. Ищем существующую запись
        var zip = await db.Zips.FindAsync(id);
        if (zip == null)
        {
            return NotFound(new { message = "Запчасть не найдена" });
        }

        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });

        // 2. Обновляем числовые поля
        var oldSellCost = zip.SellCost;

        zip.IncomeCost = request.IncomeCost;
        zip.SellCost = request.SellCost;
        zip.PartNumId = request.PartNumId;
        zip.Year = request.Year;
        zip.IncomeMotoId = request.IncomeMotoId; // Убедитесь, что фронтенд передает правильный Guid
        zip.IncomeDate = request.IncomeDate ?? zip.IncomeDate;
        zip.Comment = request.Comment;

        // Изменение цены продажи попадает в историю переоценки как наценка или уценка.
        if (request.SellCost.HasValue && oldSellCost.HasValue && request.SellCost.Value != oldSellCost.Value)
        {
            db.PriceHistories.Add(new PriceHistory
            {
                ZipId = zip.Id,
                OldCost = oldSellCost.Value,
                NewCost = request.SellCost.Value,
                OperationId = (short)(request.SellCost.Value > oldSellCost.Value
                    ? OperationEnum.Markup
                    : OperationEnum.Markdown),
                UserId = userId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                Comment = "Изменение цены через админ-панель"
            });
        }

        // 3. Обработка новых фотографий (если они были загружены)
        var photos = Request.Form.Files;
        if (photos.Count > 0)
        {
            if (photos.Count > 3)
            {
                return BadRequest(new { message = "Разрешено загружать не более 3-х фотографий." });
            }

            string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ZipPhotos");
            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            // Опционально: можно удалить старые фото перед сохранением новых
            var oldFiles = Directory.GetFiles(uploadFolder, $"{zip.Id}_*.*");
            foreach (var oldFile in oldFiles) System.IO.File.Delete(oldFile);

            await db.ZipPhotos.Where(photo => photo.ZipId == zip.Id).ExecuteDeleteAsync();

            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                if (photo.Length == 0) continue;

                string uniqueFileName = await SavePhotoAsWebpAsync(photo, uploadFolder, zip.Id, i);

                db.ZipPhotos.Add(new ZipPhoto
                {
                    ZipId = zip.Id,
                    FileName = uniqueFileName,
                    IsMain = (i == 0) // Первое фото делаем главным
                });
            }
            await db.SaveChangesAsync();
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Запись успешно обновлена", id = zip.Id });
    }

    /// <summary>Перекодирует загруженное изображение в WebP и сохраняет на диск, возвращая итоговое имя файла.</summary>
    private static async Task<string> SavePhotoAsWebpAsync(IFormFile photo, string uploadFolder, Guid zipId, int index)
    {
        string uniqueFileName = $"{zipId}_{index}.webp";
        string filePath = Path.Combine(uploadFolder, uniqueFileName);

        await using var stream = photo.OpenReadStream();
        using var image = await Image.LoadAsync(stream);
        await image.SaveAsync(filePath, new WebpEncoder { Quality = 80 });

        await ApplyWatermarkAsync(filePath);

        return uniqueFileName;
    }

    private static async Task ApplyWatermarkAsync(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath)!;
        string baseName = Path.GetFileNameWithoutExtension(filePath);
        string sourcePngPath = Path.Combine(dir, $"{baseName}_wm_src.png");
        string logoStagePath = Path.Combine(dir, $"{baseName}_wm_logo.png");
        string finalStagePath = Path.Combine(dir, $"{baseName}_wm_final.png");
        // AppContext.BaseDirectory — папка самого приложения, а не «текущая директория» процесса:
        // при dotnet run/из Visual Studio она случайно совпадает с исходниками (где Images/watermark.png
        // и лежит), но в Docker (publish + запуск из /app) файла там уже нет — из-за этого расхождения
        // логотип водяного знака в контейнере не находился.
        string logoPath = Path.Combine(AppContext.BaseDirectory, "Images", "watermark.png");

        try
        {
            using (var source = await Image.LoadAsync(filePath))
                await source.SaveAsync(sourcePngPath, new PngEncoder());

            var logoResult = await WatermarkService.ApplyImageWatermarkAsync(
                baseImagePath: sourcePngPath,
                watermarkImagePath: logoPath,
                outputPath: logoStagePath,
                position: WatermarkPosition.TopLeft,
                opacity: 0.4f,
                scale: 0.3f,
                padding: 0.02f);

            string textBasePath = logoResult.Success ? logoStagePath : sourcePngPath;

            var textResult = await WatermarkService.ApplyTextWatermarkAsync(
                baseImagePath: textBasePath,
                outputPath: finalStagePath,
                text: "DonorGarage.ru",
                fontFamily: "Arial",
                fontSize: 0.05f,
                position: WatermarkPosition.BottomRight,
                opacity: 0.4f,
                color: Color.White,
                padding: 0.05f,
                rotation: -15f);

            // На случай, если GDI+ так же молча обрубит и PNG, — не доверяем "успеху" вслепую.
            if (textResult.Success && new FileInfo(finalStagePath).Length > 512)
            {
                using var watermarked = await Image.LoadAsync(finalStagePath);
                await watermarked.SaveAsync(filePath, new WebpEncoder { Quality = 80 });
            }
        }
        finally
        {
            foreach (var temp in new[] { sourcePngPath, logoStagePath, finalStagePath })
                if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
        }
    }

    [HttpDelete("zip-photos/{photoId:int}")]
    public async Task<IActionResult> DeleteZipPhoto(int photoId)
    {
        var photo = await db.ZipPhotos.FindAsync(photoId);
        if (photo == null) return NotFound(new { message = "Фотография не найдена" });

        string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ZipPhotos");
        string filePath = Path.Combine(uploadFolder, photo.FileName);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        db.ZipPhotos.Remove(photo);
        await db.SaveChangesAsync();

        return Ok(new { message = "Фотография удалена" });
    }

    // ---------- Коррекции остатка ----------

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

        var stored = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == request.ZipId);
        if (stored == null)
        {
            stored = new Stored { ZipId = request.ZipId, Count = 0 };
            db.Stored.Add(stored);
        }

        if (stored.Count + request.Delta < 0)
            return BadRequest(new { message = $"Остаток не может стать отрицательным. Сейчас на складе: {stored.Count}" });

        stored.Count += request.Delta;

        // Отрицательная дельта — это списание, положительная — коррекция в плюс.
        db.Logs.Add(new Log
        {
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = (short)(request.Delta < 0 ? OperationEnum.WriteOff : OperationEnum.Correction),
            ZipId = zip.Id,
            UserId = CurrentUserId,
            Qty = request.Delta,
            UnitCost = zip.IncomeCost,
            Description = request.Comment.Trim()
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "Коррекция проведена", zipId = zip.Id, count = stored.Count });
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

    // Отчёты вынесены в ReportsController — Sender-у нужен доступ к ним,
    // но не ко всему остальному AdminController.

    /// <summary>
    /// Ставит одну цену продажи всем запчастям указанного парт-номера.
    /// Каждое фактическое изменение попадает в историю переоценки отдельной записью,
    /// чтобы было видно, что цена менялась массово.
    /// </summary>
    [HttpPost("reprice-part-num")]
    public async Task<IActionResult> RepricePartNum(AdminRepricePartNumRequest request)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });

        var zips = await db.Zips
            .Where(z => z.PartNumId == request.PartNumId)
            .Where(z => request.ExceptZipId == null || z.Id != request.ExceptZipId)
            .ToListAsync();

        if (zips.Count == 0)
            return Ok(new { message = "Обновлять нечего", updated = 0 });

        await using var tx = await db.Database.BeginTransactionAsync();

        var updated = 0;
        foreach (var zip in zips)
        {
            var oldCost = zip.SellCost ?? 0m;
            if (oldCost == request.NewCost) continue;

            zip.SellCost = request.NewCost;

            db.PriceHistories.Add(new PriceHistory
            {
                ZipId = zip.Id,
                OldCost = oldCost,
                NewCost = request.NewCost,
                OperationId = (short)(request.NewCost > oldCost ? OperationEnum.Markup : OperationEnum.Markdown),
                UserId = userId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                Comment = request.Comment ?? "Массовая переоценка по парт-номеру"
            });
            updated++;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = $"Цена обновлена у запчастей: {updated}", updated });
    }

    // ---------- Users ----------
    [HttpGet("users")]
    public async Task<IActionResult> Users() =>
        Ok(await db.Users.OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Email, u.FIO, u.PhoneNumber, u.IsAdmin, u.IsSender, u.IsRegistrar })
            .ToListAsync());

    [HttpPost("users")]
    public async Task<IActionResult> AddUser(AdminUserRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.FIO))
            return BadRequest(new { message = "Email и ФИО обязательны" });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        var user = new User
        {
            Email = email,
            FIO = request.FIO.Trim(),
            PhoneNumber = request.PhoneNumber,
            IsAdmin = request.IsAdmin,
            IsRegistrar = request.IsRegistrar,
            IsSender = request.IsSender,
            PasswordHash = string.IsNullOrEmpty(request.Password) ? null : PasswordHasher.Hash(request.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.FIO, user.IsAdmin });
    }

    // ---------- DeliveryAdresses ----------
    [HttpGet("addressess")]
    public async Task<IActionResult> Addressess() =>
        Ok(await db.DeliveryAddressess.OrderBy(a => a.Id) // Исправлено на DeliveryAddressess
            .Select(a => new
            {
                a.Id,
                a.Address,
                a.PostCode,
                a.UserId,
                UserEmail = a.User != null ? a.User.Email : null
            })
            .ToListAsync());

    [HttpPost("addressess")]
    public async Task<IActionResult> AddAddress(AdminAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { message = "Адрес обязателен" });
        if (request.UserId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.UserId))
            return BadRequest(new { message = "Пользователь не найден" });

        var address = new DeliveryAddress
        {
            Address = request.Address.Trim(),
            PostCode = request.PostCode,
            UserId = request.UserId,
        };
        db.DeliveryAddressess.Add(address);
        await db.SaveChangesAsync();
        return Ok(new { address.Id, address.Address });
    }

    // ---------- Orders ----------
    [HttpGet("orders")]
    public async Task<IActionResult> Orders() =>
        Ok(await db.Orders.OrderByDescending(o => o.OrderDateTime)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.CountOrdered,
                o.ZipId,
                ZipName = o.Zip.PartNumber.Name,
                PartNum = o.Zip.PartNumber.PartNum,
                o.AddressId,
                o.OrderDateTime,
                o.SellCost,
                o.OperationId,
                o.Discount,
                o.UserId,
                UserFio = o.User.FIO,
                o.DeliveryStatusId,
                DeliveryStatus = o.DeliveryStatus != null ? o.DeliveryStatus.Description : null
            })
            .ToListAsync());

    [HttpPost("orders")]
    public async Task<IActionResult> AddOrder(AdminOrderRequest request)
    {
        if (!await db.DeliveryAddressess.AnyAsync(a => a.Id == request.AddressId))
            return BadRequest(new { message = "Адрес доставки не найден" });
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId))
            return BadRequest(new { message = "Покупатель не найден" });

        var zip = await db.Zips.FirstOrDefaultAsync(z => z.Id == request.ZipId);
        if (zip == null) return BadRequest(new { message = "Запчасть не найдена" });

        await using var tx = await db.Database.BeginTransactionAsync();

        var stored = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == request.ZipId);
        if (stored == null || stored.Count < request.CountOrdered)
            return BadRequest(new { message = "Недостаточно товара на складе. Доступно: " + (stored?.Count ?? 0) });

        stored.Count -= request.CountOrdered;

        // Комментарий заказа необязателен — если пусто, генерируем номер, как это уже
        // делает публичный OrdersController для гостевых/пользовательских заказов.
        var orderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
            ? "ORD-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            : request.OrderNumber.Trim();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CountOrdered = request.CountOrdered,
            ZipId = request.ZipId,
            AddressId = request.AddressId,
            UserId = request.UserId,
            // С фронта приходит только календарная дата (input[type=date], без времени и зоны) —
            // ASP.NET достраивает её локальным смещением сервера, а Npgsql пишет timestamptz
            // только с Offset=0. ToUniversalTime() тут сдвинул бы саму дату (например, на день
            // назад), поэтому просто фиксируем выбранный день на полночь UTC, без конвертации.
            OrderDateTime = request.OrderDateTime.HasValue
                ? new DateTimeOffset(request.OrderDateTime.Value.Date, TimeSpan.Zero)
                : DateTimeOffset.UtcNow,
            SellCost = request.SellCost,
            Discount = request.Discount,
            OperationId = request.OperationId ?? (short)OperationEnum.Sale,
            DeliveryStatusId = request.DeliveryStatusId
        };
        db.Orders.Add(order);

        db.Logs.Add(new Log
        {
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = (short)OperationEnum.Sale,
            OrderId = order.Id,
            ZipId = zip.Id,
            UserId = CurrentUserId,
            Qty = -request.CountOrdered,
            UnitCost = zip.IncomeCost,
            SellCost = request.SellCost,
            Description = $"Заказ {order.OrderNumber} заведён из админ-панели"
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { order.Id, order.OrderNumber });
    }

    /// <summary>
    /// Отдаёт PDF-этикетку с QR-кодом запчасти. Генерация PDF живёт только здесь (не в AddOrder) —
    /// печатают его явно, по кнопке в «Печать QR-кода», а не автоматически при каждом заказе.
    /// Доступны только запчасти, по которым уже был хотя бы один заказ (фронт фильтрует список).
    /// </summary>
    [HttpGet("zip/{id:guid}/qr-label")]
    public async Task<IActionResult> GetZipQrLabel(Guid id)
    {
        if (!await db.Orders.AnyAsync(o => o.ZipId == id))
            return BadRequest(new { message = "По этой запчасти ещё не было заказов" });

        var zip = await db.Zips.Include(z => z.PartNumber).Include(z => z.IncomeMoto).FirstOrDefaultAsync(z => z.Id == id);
        if (zip == null) return BadRequest(new { message = "Запчасть не найдена" });

        string pdfPath = GenerateZipQrLabel(zip);
        return PhysicalFile(pdfPath, "application/pdf");
    }

    /// <summary>Генерирует PDF-этикетку с QR-кодом запчасти в wwwroot/Labels, заменяя прежний файл, если он уже был, и возвращает путь к файлу.</summary>
    private static string GenerateZipQrLabel(Zip zip)
    {
        string labelFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Labels");
        Directory.CreateDirectory(labelFolder);

        string existingPath = Path.Combine(labelFolder, $"{zip.Id}.pdf");
        if (System.IO.File.Exists(existingPath))
            System.IO.File.Delete(existingPath);

        using var qrCodePdfService = new QrCodePdfService();
        return qrCodePdfService.GenerateQrCodePdf(
            zip.Id.ToString(), zip.PartNumber.Name, zip.IncomeMoto.Description, outputFolder: labelFolder);
    }

    [HttpPut("{table}/{id}")]
    public async Task<IActionResult> Update(string table, string id, [FromBody] System.Text.Json.JsonElement payload)
    {
        // 1. Ищем сущность (DbSet) в метаданных контекста
        var entityType = db.Model.GetEntityTypes()
            .FirstOrDefault(t => t.GetTableName().Equals(table, StringComparison.OrdinalIgnoreCase)
                              || t.ClrType.Name.Equals(table, StringComparison.OrdinalIgnoreCase));

        if (entityType == null)
            return NotFound(new { message = $"Таблица '{table}' не найдена" });

        var clrType = entityType.ClrType;

        // 2. Получаем первичный ключ
        var primaryKeyProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (primaryKeyProperty == null)
            return BadRequest(new { message = $"У таблицы '{table}' отсутствует первичный ключ" });

        object parsedId;
        try
        {
            var keyType = primaryKeyProperty.ClrType;
            if (keyType == typeof(Guid))
                parsedId = Guid.Parse(id);
            else if (keyType == typeof(int))
                parsedId = int.Parse(id);
            else if (keyType == typeof(long))
                parsedId = long.Parse(id);
            else
                parsedId = id;
        }
        catch
        {
            return BadRequest(new { message = $"Некорректный формат ID '{id}'" });
        }

        // 3. Достаем запись из БД
        var entity = await db.FindAsync(clrType, parsedId);
        if (entity == null)
            return NotFound(new { message = "Запись не найдена" });

        // 4. Обновляем измененные поля на основе присланного JSON (Используем EnumerateObject)
        foreach (var prop in payload.EnumerateObject())
        {
            var name = prop.Name;
            if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
                continue; // Пропускаем изменение первичного ключа

            var clrProp = clrType.GetProperty(name, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (clrProp != null && clrProp.CanWrite)
            {
                try
                {
                    var propType = Nullable.GetUnderlyingType(clrProp.PropertyType) ?? clrProp.PropertyType;
                    var value = prop.Value;

                    if (value.ValueKind == System.Text.Json.JsonValueKind.Null)
                    {
                        clrProp.SetValue(entity, null);
                        continue;
                    }

                    object? convertedValue = null;

                    // Ручной маппинг типов System.Text.Json во внутренние типы C#
                    if (propType == typeof(string)) convertedValue = value.GetString();
                    else if (propType == typeof(int)) convertedValue = value.GetInt32();
                    else if (propType == typeof(long)) convertedValue = value.GetInt64();
                    else if (propType == typeof(double)) convertedValue = value.GetDouble();
                    else if (propType == typeof(decimal)) convertedValue = value.GetDecimal();
                    else if (propType == typeof(bool)) convertedValue = value.GetBoolean();
                    else if (propType == typeof(Guid)) convertedValue = value.GetGuid();
                    // Как и в AddOrder — фиксируем календарную дату на полночь UTC вместо
                    // конвертации через локальное смещение сервера (см. комментарий там).
                    else if (propType == typeof(DateTimeOffset)) convertedValue = new DateTimeOffset(value.GetDateTimeOffset().Date, TimeSpan.Zero);
                    else if (propType == typeof(DateTime)) convertedValue = value.GetDateTime();
                    else if (propType == typeof(DateOnly))
                    {
                        // Особый случай для работы с DateOnly (как в вашей модели Zip.Year)
                        if (value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            convertedValue = new DateOnly(value.GetInt32(), 1, 1);
                        }
                        else if (value.ValueKind == System.Text.Json.JsonValueKind.String && DateOnly.TryParse(value.GetString(), out var d))
                        {
                            convertedValue = d;
                        }
                    }
                    else
                    {
                        convertedValue = System.Text.Json.JsonSerializer.Deserialize(value.GetRawText(), clrProp.PropertyType);
                    }

                    clrProp.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = $"Ошибка валидации поля '{name}': {ex.Message}" });
                }
            }
        }

        // 5. Сохраняем изменения
        db.Entry(entity).State = EntityState.Modified;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Ошибка БД при сохранении: {ex.InnerException?.Message ?? ex.Message}" });
        }

        return Ok(entity);
    }

    [HttpGet("users/search")]
    public async Task<ActionResult> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new List<object>());

        var query = q.ToLowerInvariant().Trim();

        // Ищем по частичному совпадению почты или телефона
        var users = await db.Users
            .Where(u => u.Email.ToLower().Contains(query) ||
                       (u.PhoneNumber != null && u.PhoneNumber.Contains(query)))
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FIO,
                u.PhoneNumber,
                u.IsSender,
                u.IsRegistrar,
                u.IsAdmin,
            })
            .Take(10) // Ограничиваем выдачу, чтобы не грузить базу
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("users/{id}/roles")]
    public async Task<ActionResult> UpdateUserRoles(int id, [FromBody] UpdateUserRolesRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        // Обновляем значения
        user.IsSender = request.IsSender;
        user.IsRegistrar = request.IsRegistrar;

        await db.SaveChangesAsync();

        return Ok(new { message = "Права пользователя обновлены" });
    }

    [HttpGet("incomemotos")]
    public async Task<ActionResult> GetIncomeMotos()
    {
        var donors = await db.IncomeMotos.ToListAsync();
        return Ok(donors);
    }

    [HttpPost("incomemotos")]
    public async Task<ActionResult> AddIncomeMoto([FromBody] IncomeMoto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(new { message = "Описание не может быть пустым" });
        if (dto.UserId.HasValue && !await db.Users.AnyAsync(u => u.Id == dto.UserId))
            return BadRequest(new { message = "Поставщик не найден" });

        var newDonor = new IncomeMoto
        {
            Id = Guid.NewGuid(), // Генерируем UUID (uuid)
            Description = dto.Description,
            UserId = dto.UserId
        };

        db.IncomeMotos.Add(newDonor);
        await db.SaveChangesAsync();

        return Ok(newDonor);
    }

    [HttpPut("incomemotos/{id}")]
    public async Task<ActionResult> UpdateIncomeMoto(Guid id, [FromBody] IncomeMoto dto)
    {
        var donor = await db.IncomeMotos.FindAsync(id);
        if (donor == null) return NotFound(new { message = "Донор не найден" });
        if (dto.UserId.HasValue && !await db.Users.AnyAsync(u => u.Id == dto.UserId))
            return BadRequest(new { message = "Поставщик не найден" });

        donor.Description = dto.Description;
        donor.UserId = dto.UserId;
        await db.SaveChangesAsync();

        return Ok(donor);
    }

    /// <summary>
    /// Удаление записи вместе с зависимыми данными.
    ///
    /// Подчищается только то, что без родителя теряет смысл (остаток, фотографии,
    /// записи журнала, применимость). Ссылки, за которыми стоят реальные документы —
    /// прежде всего заказы, — не удаляются: вместо этого возвращается 409 с
    /// объяснением, что именно мешает. Иначе одно нажатие «Удалить» на запчасти
    /// стирало бы историю продаж.
    /// </summary>
    [HttpDelete("{endpoint}/{id}")]
    public async Task<IActionResult> DeleteEntity(string endpoint, string id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        switch (endpoint.ToLower())
        {
            case "marks":
            {
                if (!int.TryParse(id, out var markId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var mark = await db.MotoMarks.FindAsync(markId);
                if (mark == null) return NotFound();

                // Модели остаются, но теряют привязку к марке (MarkId допускает null).
                await db.MotoModels.Where(m => m.MarkId == markId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.MarkId, (int?)null));

                db.MotoMarks.Remove(mark);
                break;
            }

            case "models":
            {
                if (!int.TryParse(id, out var modelId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var model = await db.MotoModels.FindAsync(modelId);
                if (model == null) return NotFound();

                // Применимость к удаляемой модели смысла не имеет.
                await db.PartNumberApplicabilities.Where(a => a.ModelId == modelId).ExecuteDeleteAsync();

                db.MotoModels.Remove(model);
                break;
            }

            case "groups":
            {
                if (!int.TryParse(id, out var groupId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var group = await db.ZipGroups.FindAsync(groupId);
                if (group == null) return NotFound();

                // Каталожные позиции остаются, просто без группы.
                await db.PartNumbers.Where(p => p.GroupId == groupId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.GroupId, (int?)null));

                db.ZipGroups.Remove(group);
                break;
            }

            case "partnumbers":
            case "part-numbers":
            {
                if (!int.TryParse(id, out var partNumId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var pn = await db.PartNumbers.FindAsync(partNumId);
                if (pn == null) return NotFound();

                var zipCount = await db.Zips.CountAsync(z => z.PartNumId == partNumId);
                if (zipCount > 0)
                    return Conflict(new { message = $"По этому парт-номеру заведено запчастей: {zipCount}. Сначала удалите их." });

                await db.PartNumberApplicabilities.Where(a => a.PartNumId == partNumId).ExecuteDeleteAsync();
                await db.PartNumberSeriesApplicabilities.Where(a => a.PartNumId == partNumId).ExecuteDeleteAsync();

                db.PartNumbers.Remove(pn);
                break;
            }

            case "applicability":
            {
                if (!int.TryParse(id, out var linkId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var link = await db.PartNumberApplicabilities.FindAsync(linkId);
                if (link == null) return NotFound();
                db.PartNumberApplicabilities.Remove(link);
                break;
            }

            case "series":
            {
                if (!Guid.TryParse(id, out var seriesGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var series = await db.MotoSeries.FindAsync(seriesGuid);
                if (series == null) return NotFound();

                // Применимость к удаляемой серии смысла не имеет.
                await db.PartNumberSeriesApplicabilities.Where(a => a.SeriesId == seriesGuid).ExecuteDeleteAsync();

                db.MotoSeries.Remove(series);
                break;
            }

            case "series-applicability":
            {
                if (!int.TryParse(id, out var seriesLinkId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var seriesLink = await db.PartNumberSeriesApplicabilities.FindAsync(seriesLinkId);
                if (seriesLink == null) return NotFound();
                db.PartNumberSeriesApplicabilities.Remove(seriesLink);
                break;
            }

            case "zip":
            {
                if (!Guid.TryParse(id, out var zipGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var zip = await db.Zips.FindAsync(zipGuid);
                if (zip == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.ZipId == zipGuid);
                if (orderCount > 0)
                    return Conflict(new { message = $"На эту запчасть ссылаются заказы: {orderCount}. Удалить нельзя — иначе из истории продаж пропадёт документ." });

                // Файлы фотографий удаляем с диска до того, как исчезнут строки в БД.
                var photoNames = await db.ZipPhotos.Where(p => p.ZipId == zipGuid).Select(p => p.FileName).ToListAsync();
                var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ZipPhotos");
                foreach (var name in photoNames)
                {
                    var filePath = Path.Combine(uploadFolder, name);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }

                await db.ZipPhotos.Where(p => p.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.PriceHistories.Where(p => p.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.Logs.Where(l => l.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.Stored.Where(s => s.ZipId == zipGuid).ExecuteDeleteAsync();

                db.Zips.Remove(zip);
                break;
            }

            case "users":
            {
                if (!int.TryParse(id, out var userId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var user = await db.Users.FindAsync(userId);
                if (user == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.UserId == userId);
                if (orderCount > 0)
                    return Conflict(new { message = $"У пользователя есть заказы: {orderCount}. Удалить нельзя — вместе с ним пропали бы документы." });

                // PriceHistory.UserId не допускает null, поэтому пользователя,
                // проводившего переоценку, удалить нельзя без потери истории цен.
                var repriceCount = await db.PriceHistories.CountAsync(p => p.UserId == userId);
                if (repriceCount > 0)
                    return Conflict(new { message = $"Пользователь проводил переоценку ({repriceCount} записей в истории цен). Удалить нельзя." });

                // В журнале и у доноров автор остаётся неизвестным — поля допускают null.
                await db.Logs.Where(l => l.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.UserId, (int?)null));
                await db.IncomeMotos.Where(i => i.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.UserId, (int?)null));

                await db.DeliveryAddressess.Where(a => a.UserId == userId).ExecuteDeleteAsync();

                db.Users.Remove(user);
                break;
            }

            case "addressess":
            {
                if (!int.TryParse(id, out var addressId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var address = await db.DeliveryAddressess.FindAsync(addressId);
                if (address == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.AddressId == addressId);
                if (orderCount > 0)
                    return Conflict(new { message = $"На этот адрес оформлены заказы: {orderCount}. Удалить нельзя." });

                db.DeliveryAddressess.Remove(address);
                break;
            }

            case "orders":
            {
                if (!Guid.TryParse(id, out var orderGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var order = await db.Orders.Include(o => o.DeliveryStatus).FirstOrDefaultAsync(o => o.Id == orderGuid);
                if (order == null) return NotFound();

                var statusDescription = order.DeliveryStatus?.Description;

                // "canceled" уже вернул остаток на склад при смене статуса (см. SenderController.UpdateStatus) —
                // повторный возврат задвоил бы количество. "completed" — товар реально отгружен покупателю,
                // удаление документа задним числом не должно магически возвращать его на склад.
                // Во всех остальных случаях (null, created, sent) заказ ещё не завершён — резерв возвращаем.
                if (statusDescription != "completed" && statusDescription != "canceled")
                {
                    var storedItem = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == order.ZipId);
                    if (storedItem != null)
                        storedItem.Count += order.CountOrdered;
                    else
                        db.Stored.Add(new Stored { ZipId = order.ZipId, Count = order.CountOrdered });

                    db.Logs.Add(new Log
                    {
                        CreatedAt = DateTimeOffset.UtcNow,
                        OperationId = (short)OperationEnum.Refund,
                        ZipId = order.ZipId,
                        UserId = CurrentUserId,
                        Qty = order.CountOrdered,
                        SellCost = order.SellCost,
                        Description = $"Возврат на склад: незавершённый заказ {order.OrderNumber} удалён из админ-панели"
                    });
                }

                // Записи журнала по этому заказу уходят вместе с ним (кроме только что добавленной — она не привязана к OrderId).
                await db.Logs.Where(l => l.OrderId == orderGuid).ExecuteDeleteAsync();

                db.Orders.Remove(order);
                break;
            }

            case "incomemotos":
            {
                if (!Guid.TryParse(id, out var donorGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var donor = await db.IncomeMotos.FindAsync(donorGuid);
                if (donor == null) return NotFound();

                var zipCount = await db.Zips.CountAsync(z => z.IncomeMotoId == donorGuid);
                if (zipCount > 0)
                    return Conflict(new { message = $"С этого донора заведено запчастей: {zipCount}. Сначала удалите их." });

                db.IncomeMotos.Remove(donor);
                break;
            }

            case "logs":
            {
                if (!int.TryParse(id, out var logId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var log = await db.Logs.FindAsync(logId);
                if (log == null) return NotFound();
                db.Logs.Remove(log);
                break;
            }

            case "deliverystatuses":
            {
                if (!short.TryParse(id, out var statusId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var ds = await db.DeliveryStatuses.FindAsync(statusId);
                if (ds == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.DeliveryStatusId == statusId);
                if (orderCount > 0)
                    return Conflict(new { message = $"Статус используется в заказах: {orderCount}. Удалить нельзя." });

                db.DeliveryStatuses.Remove(ds);
                break;
            }

            default:
                return BadRequest(new { message = "Неизвестный эндпоинт" });
        }

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { message = "Запись успешно удалена" });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BadRequest(new { message = "Невозможно удалить запись, так как на нее ссылаются другие данные", error = ex.InnerException?.Message ?? ex.Message });
        }
    }
}

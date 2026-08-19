using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
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

/// <summary>Склад: запчасти, фотографии, коррекции остатка, переоценка, доноры, QR-этикетки.</summary>
public class AdminWarehouseController(AppDbContext db, WarehouseService warehouse) : AdminControllerBase
{
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

        // Остаток и приходное движение создаются вместе с деталью — одной операцией.
        var receipt = await warehouse.ReceiveAsync(
            zip, request.CountStored, CurrentUserId,
            $"Оприходование: {partNumber.Name} ({partNumber.PartNum})");

        if (receipt.IsFailure) return receipt.Error!.ToErrorResponse();

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

    [HttpGet("zip/{id:guid}/qr-label")]
    public async Task<IActionResult> GetZipQrLabel(Guid id)
    {
        // Прежний вариант: этикетку можно было печатать только по запчасти, на которую уже был
        // хотя бы один заказ. Сейчас не используется — печатаем по наличию самой запчасти.
        // Оставлено на случай, если ограничение понадобится вернуть.
        // if (!await db.Orders.AnyAsync(o => o.ZipId == id))
        //     return BadRequest(new { message = "По этой запчасти ещё не было заказов" });

        var zip = await db.Zips.Include(z => z.PartNumber).Include(z => z.IncomeMoto).FirstOrDefaultAsync(z => z.Id == id);
        if (zip == null) return BadRequest(new { message = "Запчасть не найдена" });

        string pdfPath = GenerateZipQrLabel(zip);

        // Каждый клик должен отдавать только что сгенерированный файл — запрещаем браузеру
        // и промежуточным прокси отдавать закэшированную версию по ETag/Last-Modified.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        return PhysicalFile(pdfPath, "application/pdf", enableRangeProcessing: false);
    }

    /// <summary>
    /// Генерирует PDF-этикетку с QR-кодом запчасти в wwwroot/Labels. В папке всегда держим не
    /// больше одного файла — перед генерацией удаляем всё, что там лежало (в т.ч. этикетки для
    /// других запчастей), и пишем под одним и тем же именем.
    /// </summary>
    private static string GenerateZipQrLabel(Zip zip)
    {
        const string labelFileName = "qr-label.pdf";

        string labelFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Labels");
        Directory.CreateDirectory(labelFolder);

        foreach (var oldFile in Directory.GetFiles(labelFolder))
            System.IO.File.Delete(oldFile);

        using var qrCodePdfService = new QrCodePdfService();
        return qrCodePdfService.GenerateQrCodePdf(
            zip.Id.ToString(), zip.PartNumber.Name, zip.IncomeMoto.Description,
            outputFolder: labelFolder, fileName: labelFileName);
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
}

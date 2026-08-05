using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;
using MotoParts.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MotoParts.Api.Controllers;

/// <summary>Админ-панель: ручное добавление записей в каждую таблицу и просмотр содержимого.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin   ,Registrar")]
public class AdminController(AppDbContext db) : ControllerBase
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
        Ok(await db.PartNumbers.OrderBy(p => p.Id).Select(p => new { p.Id, p.PartNum }).ToListAsync());

    [HttpPost("part-numbers")]
    public async Task<IActionResult> AddPartNumber(AdminPartNumberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PartNum))
            return BadRequest(new { message = "Номер запчасти обязателен" });

        var pn = new PartNumber { PartNum = request.PartNum.Trim() };
        db.PartNumbers.Add(pn);
        await db.SaveChangesAsync();
        return Ok(new { pn.Id, pn.PartNum });
    }

    // ---------- Zip ----------
    [HttpGet("zip")]
    public async Task<IActionResult> Zips() =>
        Ok(await db.Zips.OrderBy(z => z.Name)
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.IncomeCost,
                z.PartNumId, // Обновлено поле
                PartNum = z.PartNumber != null ? z.PartNumber.PartNum : null,
                z.MarkId,
                Mark = z.Mark != null ? z.Mark.Mark : null,
                z.ModelId,
                Model = z.Model != null ? z.Model.Model : null,
                z.GroupId,
                Group = z.Group != null ? z.Group.GroupName : null,
                z.Year,
                z.IncomeMotoId,
                IncomeMoto = z.IncomeMoto != null ? z.IncomeMoto.Description : null,
                Photos = z.Photos.Select(p => new { p.Id, p.FileName, p.IsMain }).ToList()
            })
            .ToListAsync());

    [HttpPost("zip")]
    public async Task<IActionResult> AddZip([FromForm] AdminZipRequest request)
    {
        var zip = new Zip
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            IncomeCost = request.IncomeCost,
            PartNumId = request.PartNumId, // Обновлено поле
            MarkId = request.MarkId,
            ModelId = request.ModelId,
            GroupId = request.GroupId,
            Year = request.Year.HasValue ? (uint)request.Year.Value : null,
            IncomeMotoId = request.IncomeMotoId
        };

        db.Zips.Add(zip);
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

        return Ok(new { zip.Id, zip.Name });
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

        // 2. Обновляем текстовые и числовые поля
        zip.Name = request.Name?.Trim() ?? zip.Name;
        zip.IncomeCost = request.IncomeCost;
        zip.PartNumId = request.PartNumId;
        zip.MarkId = request.MarkId;
        zip.ModelId = request.ModelId;
        zip.GroupId = request.GroupId;
        zip.Year = request.Year.HasValue ? (uint)request.Year.Value : null;
        zip.IncomeMotoId = request.IncomeMotoId; // Убедитесь, что фронтенд передает правильный Guid

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

        return uniqueFileName;
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
                o.NomenclatureId,
                NomenclatureName = o.Nomenclature != null ? o.Nomenclature.Name : null,
                o.AddressId,
                o.OrderDateTime,
                o.SellCost,         
                o.OperationTypeId,  
                o.Discount,         
                o.UserId,           
                o.DeliveryStatusId, 
                DeliveryStatus = o.DeliveryStatus != null ? o.DeliveryStatus.Description : null
            })
            .ToListAsync());

    [HttpPost("orders")]
    public async Task<IActionResult> AddOrder(AdminOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber))
            return BadRequest(new { message = "Номер заказа обязателен" });
        if (!await db.DeliveryAddressess.AnyAsync(a => a.Id == request.AddressId))
            return BadRequest(new { message = "Адрес доставки не найден" });
        if (!await db.Zips.AnyAsync(z => z.Id == request.NomenclatureId))
            return BadRequest(new { message = "Запчасть не найдена" });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = request.OrderNumber.Trim(),
            CountOrdered = request.CountOrdered,
            NomenclatureId = request.NomenclatureId,
            AddressId = request.AddressId,
            OrderDateTime = request.OrderDateTime ?? DateTimeOffset.UtcNow,
            Discount = request.Discount
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.OrderNumber });
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
                    else if (propType == typeof(DateTimeOffset)) convertedValue = value.GetDateTimeOffset();
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

        var newDonor = new IncomeMoto
        {
            Id = Guid.NewGuid(), // Генерируем UUID (uuid)
            Description = dto.Description
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

        donor.Description = dto.Description;
        await db.SaveChangesAsync();

        return Ok(donor);
    }

    [HttpDelete("{endpoint}/{id}")]
    public async Task<IActionResult> DeleteEntity(string endpoint, string id)
    {
        // Если у вас есть система авторизации через JWT/Cookies,
        // проверку можно настроить через [Authorize(Roles = "Admin")] 
        // или вручную проверить флаг IsAdmin текущего пользователя:
        // var currentUser = await GetCurrentUserAsync();
        // if (!currentUser.IsAdmin) return Forbid();

        switch (endpoint.ToLower())
        {
            case "marks":
                var mark = await db.MotoMarks.FindAsync(int.Parse(id));
                if (mark == null) return NotFound();
                db.MotoMarks.Remove(mark);
                break;

            case "models":
                var model = await db.MotoModels.FindAsync(int.Parse(id));
                if (model == null) return NotFound();
                db.MotoModels.Remove(model);
                break;

            case "groups":
                var group = await db.ZipGroups.FindAsync(int.Parse(id));
                if (group == null) return NotFound();
                db.ZipGroups.Remove(group);
                break;

            case "partnumbers":
                var pn = await db.PartNumbers.FindAsync(int.Parse(id));
                if (pn == null) return NotFound();
                db.PartNumbers.Remove(pn);
                break;

            case "zip":
                if (!Guid.TryParse(id, out var zipGuid)) return BadRequest("Неверный формат GUID");
                var zip = await db.Zips.FindAsync(zipGuid);
                if (zip == null) return NotFound();
                db.Zips.Remove(zip);
                break;

            case "users":
                var user = await db.Users.FindAsync(int.Parse(id));
                if (user == null) return NotFound();
                db.Users.Remove(user);
                break;

            case "addressess":
                var address = await db.DeliveryAddressess.FindAsync(int.Parse(id)); // Исправлено на DeliveryAddressess
                if (address == null) return NotFound();
                db.DeliveryAddressess.Remove(address);
                break;

            case "orders":
                if (!Guid.TryParse(id, out var orderGuid)) return BadRequest("Неверный формат GUID");
                var order = await db.Orders.FindAsync(orderGuid);
                if (order == null) return NotFound();
                db.Orders.Remove(order);
                break;

            case "incomemotos":
                if (!Guid.TryParse(id, out var donorGuid)) return BadRequest("Неверный формат GUID");
                var donor = await db.IncomeMotos.FindAsync(donorGuid);
                if (donor == null) return NotFound();
                db.IncomeMotos.Remove(donor);
                break;

            case "logs":
                var log = await db.Logs.FindAsync(int.Parse(id));
                if (log == null) return NotFound();
                db.Logs.Remove(log);
                break;

            case "deliverystatuses":
                var ds = await db.DeliveryStatuses.FindAsync(short.Parse(id));
                if (ds == null) return NotFound();
                db.DeliveryStatuses.Remove(ds);
                break;

            default:
                return BadRequest(new { message = "Неизвестный эндпоинт" });
        }

        try
        {
            await db.SaveChangesAsync();
            return Ok(new { message = "Запись успешно удалена" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Невозможно удалить запись, так как на нее ссылаются другие данные", error = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Common;
using MotoParts.Domain.Models;
using MotoParts.Application.Warehouse;
using MotoParts.Infrastructure.Services;

using System.Linq;
using System.Security.Claims;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(AppDbContext db, WarehouseService warehouse, IConfiguration configuration) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Разрешённые типы файлов чека — фото или скан/PDF квитанции.</summary>
    private static readonly HashSet<string> AllowedReceiptContentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private const long MaxReceiptFileSize = 10 * 1024 * 1024; // 10 МБ — с запасом на фото со смартфона

    /// <summary>
    /// Номер карты для ручного перевода. Анонимный доступ — гость ещё не залогинен
    /// (guest-order не выдаёт токен), а карту нужно показать сразу после оформления заказа.
    /// </summary>
    [HttpGet("payment-info")]
    [AllowAnonymous]
    public IActionResult PaymentInfo()
    {
        var cardNumber = configuration["Payment:CardNumber"];
        if (string.IsNullOrWhiteSpace(cardNumber))
            return Error.NotFound("Номер карты для оплаты не настроен").ToErrorResponse();

        return Ok(new PaymentInfoDto(cardNumber));
    }

    /// <summary>
    /// Покупатель прикладывает чек о переводе. Анонимный доступ по той же причине, что и
    /// PaymentInfo — заказ уже существует (id непубличный, генерируется сервером), поэтому
    /// доступ к конкретному заказу по его Guid — тот же уровень защиты, что и у остальных
    /// точечных gets в этом приложении (см. SenderController.GetZipInfo).
    /// </summary>
    [HttpPost("{id:guid}/receipt")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadReceipt(Guid id, IFormFile file)
    {
        var order = await db.Orders.FindAsync(id);
        if (order == null) return NotFound(new { message = "Заказ не найден" });

        if (file == null || file.Length == 0)
            return Error.Validation("Файл чека не передан").ToErrorResponse();
        if (file.Length > MaxReceiptFileSize)
            return Error.Validation("Файл чека слишком большой (максимум 10 МБ)").ToErrorResponse();
        if (!AllowedReceiptContentTypes.Contains(file.ContentType))
            return Error.Validation("Чек должен быть изображением (JPEG/PNG/WebP) или PDF").ToErrorResponse();

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Receipts");
        Directory.CreateDirectory(uploadFolder);

        // Один чек на заказ — новый файл заменяет предыдущий, если покупатель прикладывает повторно.
        if (!string.IsNullOrEmpty(order.ReceiptFileName))
        {
            var oldPath = Path.Combine(uploadFolder, order.ReceiptFileName);
            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
        }

        var extension = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => Path.GetExtension(file.FileName)
        };
        var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var filePath = Path.Combine(uploadFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        order.ReceiptFileName = fileName;
        await db.SaveChangesAsync();

        return Ok(new UploadReceiptResponse(fileName));
    }

    /// <summary>Заказы текущего пользователя, пагинация по 10 штук.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<PagedResult<OrderDto>>> My([FromQuery] int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);
        var userId = CurrentUserId;

        var orders = db.Orders
            .Where(o => o.Address.UserId == userId)
            .OrderByDescending(o => o.OrderDateTime);

        var total = await orders.CountAsync();
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id, o.OrderNumber, o.CountOrdered, o.OrderDateTime,
                o.Zip.PartNumber.Name,
                o.Zip.IncomeCost,
                o.Address.Address,
                o.SellCost, o.Discount, o.IsPaid))
            .ToListAsync();

        return Ok(new PagedResult<OrderDto>(items, total, page, pageSize));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        var zip = await db.Zips.Include(z => z.PartNumber).FirstOrDefaultAsync(z => z.Id == request.ZipId);
        if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

        var address = await db.DeliveryAddressess.FindAsync(request.AddressId);
        if (address == null) return NotFound(new { message = "Адрес доставки не найден" });

        // Списание остатка и запись движения должны быть атомарны.
        await using var tx = await db.Database.BeginTransactionAsync();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-" + DateTimeOffset.Now.ToUnixTimeSeconds(),
            CountOrdered = request.Count,
            ZipId = request.ZipId,
            AddressId = request.AddressId,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = request.SellCost,
            UserId = CurrentUserId,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created,
            DeliveryCompany = request.DeliveryCompany,
            DeliveryComment = request.DeliveryComment
        };

        db.Orders.Add(order);

        var stock = await warehouse.ReserveForOrderAsync(
            zip, request.Count, order.Id, order.OrderNumber, request.SellCost, order.UserId,
            $"Заказ {order.OrderNumber}: {zip.PartNumber.Name} × {request.Count}");

        if (stock.IsFailure) return stock.Error!.ToErrorResponse();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // Не отдаём наружу саму сущность Order: её навигационные свойства (Zip.PartNumber.Zips
        // ссылается на тот же Zip) образуют цикл, который System.Text.Json не умеет
        // сериализовать (см. регрессионный тест ниже).
        return Ok(new OrderDto(
            order.Id, order.OrderNumber, order.CountOrdered, order.OrderDateTime,
            zip.PartNumber.Name, zip.IncomeCost, address.Address,
            order.SellCost, order.Discount, order.IsPaid));
    }

    [HttpDelete("orders/{id}")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        var order = await db.Orders
            // Если у вас есть связанные оплаты, EF Core удалит их каскадно (если настроено)
            // или их нужно будет включить и удалить явно, например: .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound(new { message = "Заказ не найден" });

        db.Orders.Remove(order);
        await db.SaveChangesAsync();

        return Ok(new { message = "Заказ успешно удален" });
    }

    // Оформление без регистрации: на контроллере висит [Authorize], поэтому
    // анонимный доступ открывается точечно, иначе гостевой заказ отдаёт 401.
    [AllowAnonymous]
    [HttpPost("guest-order")]
    public async Task<ActionResult<OrderDto>> CreateGuestOrder([FromBody] GuestCreateOrderRequest req)
    {
        // 1. Ищем запчасть
        var zip = await db.Zips.Include(z => z.PartNumber).FirstOrDefaultAsync(z => z.Id == req.ZipId);
        if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

        // 2. Проверяем пользователя по номеру телефона
        var user = await db.Users
            .Include(u => u.DeliveryAddresses)
            .FirstOrDefaultAsync(u => u.PhoneNumber == req.Phone);

        if (user == null)
        {
            // Пользователь не найден -> создаем нового
            user = new User
            {
                Email = req.Email,
                PhoneNumber = req.Phone,
                FIO = req.Fio,
                PasswordHash = !string.IsNullOrEmpty(req.Password)
                    ? PasswordHasher.Hash(req.Password) // Используйте ваш сервис хэширования
                    : "", // Или сгенерируйте случайный пароль
                DeliveryAddresses = new List<DeliveryAddress>()
            };
            db.Users.Add(user);
        }

        // 3. Проверяем адрес пользователя
        var address = user.DeliveryAddresses?
            .FirstOrDefault(a => a.Address == req.Address && a.PostCode == req.PostCode);

        if (address == null)
        {
            // Если адреса нет, создаем его
            address = new DeliveryAddress
            {
                Address = req.Address,
                PostCode = req.PostCode,
                User = user
            };
            db.DeliveryAddressess.Add(address);
        }

        // Сохраняем изменения, чтобы получить сгенерированные ID для пользователя и адреса
        await db.SaveChangesAsync();
        user = await db.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == req.Phone);
        // 4. Создаем заказ с операцией расхода
        await using var tx = await db.Database.BeginTransactionAsync();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            CountOrdered = req.Count,
            ZipId = zip.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = zip.SellCost ?? 0m,
            Discount = req.Promo,
            UserId = user.Id,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = (short)DeliveryStatusEnum.created,
            DeliveryCompany = req.DeliveryCompany,
            DeliveryComment = req.DeliveryComment
        };
        db.Orders.Add(order);

        var incomeMoto = await db.IncomeMotos.FirstOrDefaultAsync(x => x.Id == zip.IncomeMotoId);

        // 5. Списание со склада и запись в журнал — одной операцией
        var stock = await warehouse.ReserveForOrderAsync(
            zip, req.Count, order.Id, order.OrderNumber, order.SellCost, user.Id,
            $"Заказ {order.OrderNumber} для {user.PhoneNumber}:{user.FIO}. " +
            $"Деталь {zip.PartNumber.Name} взята с мотоцикла {incomeMoto?.Description ?? "—"}");

        if (stock.IsFailure) return stock.Error!.ToErrorResponse();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // Формируем ответ (аналогичный существующему методу создания)
        var dto = new OrderDto(
            order.Id,
            order.OrderNumber,
            order.CountOrdered,
            order.OrderDateTime,
            zip.PartNumber.Name,
            zip.IncomeCost,
            address.Address,
            order.SellCost,
            order.Discount,
            order.IsPaid
        );

        return Ok(dto);
    }
}

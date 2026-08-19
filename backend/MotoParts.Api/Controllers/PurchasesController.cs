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

using System.Security.Claims;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Оформление покупки — из одной позиции («Купить» на карточке товара) или из нескольких
/// (корзина). Обе ветки идут через один и тот же Checkout: разница только в количестве
/// элементов Items. Адрес, доставка и оплата — общие на всю покупку (см. Purchase),
/// количество и склад списываются по каждой позиции отдельно (см. WarehouseService).
/// </summary>
[ApiController]
[Route("api/purchases")]
[Authorize]
public class PurchasesController(AppDbContext db, WarehouseService warehouse, IConfiguration configuration) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Разрешённые типы файлов чека — фото или скан/PDF квитанции.</summary>
    private static readonly HashSet<string> AllowedReceiptContentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private const long MaxReceiptFileSize = 10 * 1024 * 1024; // 10 МБ — с запасом на фото со смартфона

    /// <summary>
    /// Номер карты для ручного перевода. Анонимный доступ — гость ещё не залогинен
    /// (guest-checkout не выдаёт токен), а карту нужно показать сразу после оформления.
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
    /// Покупатель прикладывает чек о переводе — один на всю покупку, а не на каждую позицию.
    /// Анонимный доступ по той же причине, что и PaymentInfo — id покупки непубличный,
    /// генерируется сервером, поэтому доступ по её Guid — тот же уровень защиты, что и у
    /// остальных точечных gets в этом приложении (см. SenderController.GetZipInfo).
    /// </summary>
    [HttpPost("{id:guid}/receipt")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadReceipt(Guid id, IFormFile file)
    {
        var purchase = await db.Purchases.FindAsync(id);
        if (purchase == null) return NotFound(new { message = "Покупка не найдена" });

        if (file == null || file.Length == 0)
            return Error.Validation("Файл чека не передан").ToErrorResponse();
        if (file.Length > MaxReceiptFileSize)
            return Error.Validation("Файл чека слишком большой (максимум 10 МБ)").ToErrorResponse();
        if (!AllowedReceiptContentTypes.Contains(file.ContentType))
            return Error.Validation("Чек должен быть изображением (JPEG/PNG/WebP) или PDF").ToErrorResponse();

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Receipts");
        Directory.CreateDirectory(uploadFolder);

        // Один чек на покупку — новый файл заменяет предыдущий, если покупатель прикладывает повторно.
        if (!string.IsNullOrEmpty(purchase.ReceiptFileName))
        {
            var oldPath = Path.Combine(uploadFolder, purchase.ReceiptFileName);
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

        purchase.ReceiptFileName = fileName;
        await db.SaveChangesAsync();

        return Ok(new UploadReceiptResponse(fileName));
    }

    /// <summary>Оформление покупки авторизованным пользователем — из одной позиции или из корзины.</summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseDto>> Checkout(CheckoutRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return Error.Validation("Корзина пуста").ToErrorResponse();

        var address = await db.DeliveryAddressess.FindAsync(request.AddressId);
        if (address == null) return NotFound(new { message = "Адрес доставки не найден" });

        // Списание остатка и запись движения должны быть атомарны для каждой позиции,
        // а сама покупка — атомарна целиком: либо резервируются все позиции, либо ни одной.
        await using var tx = await db.Database.BeginTransactionAsync();

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UserId = CurrentUserId,
            AddressId = request.AddressId,
            OrderDateTime = DateTimeOffset.UtcNow,
            DeliveryCompany = request.DeliveryCompany,
            DeliveryComment = request.DeliveryComment
        };
        db.Purchases.Add(purchase);

        var items = new List<OrderDto>();
        for (var i = 0; i < request.Items.Count; i++)
        {
            var line = request.Items[i];
            var zip = await db.Zips.Include(z => z.PartNumber).FirstOrDefaultAsync(z => z.Id == line.ZipId);
            if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{i}",
                CountOrdered = line.Count,
                ZipId = line.ZipId,
                OrderDateTime = purchase.OrderDateTime,
                SellCost = line.SellCost,
                OperationId = (short)OperationEnum.Sale,
                DeliveryStatusId = (short)DeliveryStatusEnum.created,
                PurchaseId = purchase.Id
            };
            db.Orders.Add(order);

            var stock = await warehouse.ReserveForOrderAsync(
                zip, line.Count, order.Id, order.OrderNumber, line.SellCost, purchase.UserId,
                $"Заказ {order.OrderNumber}: {zip.PartNumber.Name} × {line.Count}");

            if (stock.IsFailure) return stock.Error!.ToErrorResponse();

            // Не отдаём наружу саму сущность Order: её навигационные свойства образуют цикл,
            // который System.Text.Json не умеет сериализовать (см. регрессионный тест).
            items.Add(new OrderDto(
                order.Id, order.OrderNumber, order.CountOrdered, order.OrderDateTime,
                zip.PartNumber.Name, zip.IncomeCost, address.Address,
                order.SellCost, order.Discount, purchase.IsPaid, purchase.ReceiptFileName,
                purchase.Id, purchase.PurchaseNumber));
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new PurchaseDto(purchase.Id, purchase.PurchaseNumber, items));
    }

    // Оформление без регистрации: на контроллере висит [Authorize], поэтому
    // анонимный доступ открывается точечно, иначе гостевая покупка отдаёт 401.
    [AllowAnonymous]
    [HttpPost("guest")]
    public async Task<ActionResult<PurchaseDto>> GuestCheckout([FromBody] GuestCheckoutRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return Error.Validation("Корзина пуста").ToErrorResponse();

        // 1. Проверяем пользователя по номеру телефона
        var user = await db.Users
            .Include(u => u.DeliveryAddresses)
            .FirstOrDefaultAsync(u => u.PhoneNumber == req.Phone);

        if (user == null)
        {
            // Пользователь не найден -> создаём нового
            user = new User
            {
                Email = req.Email,
                PhoneNumber = req.Phone,
                FIO = req.Fio,
                PasswordHash = !string.IsNullOrEmpty(req.Password)
                    ? PasswordHasher.Hash(req.Password)
                    : "",
                DeliveryAddresses = new List<DeliveryAddress>()
            };
            db.Users.Add(user);
        }

        // 2. Проверяем адрес пользователя
        var address = user.DeliveryAddresses?
            .FirstOrDefault(a => a.Address == req.Address && a.PostCode == req.PostCode);

        if (address == null)
        {
            address = new DeliveryAddress
            {
                Address = req.Address,
                PostCode = req.PostCode,
                User = user
            };
            db.DeliveryAddressess.Add(address);
        }

        // Сохраняем, чтобы получить сгенерированные id пользователя и адреса
        await db.SaveChangesAsync();
        user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == req.Phone);

        await using var tx = await db.Database.BeginTransactionAsync();

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UserId = user!.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            DeliveryCompany = req.DeliveryCompany,
            DeliveryComment = req.DeliveryComment
        };
        db.Purchases.Add(purchase);

        var items = new List<OrderDto>();
        for (var i = 0; i < req.Items.Count; i++)
        {
            var line = req.Items[i];
            var zip = await db.Zips.Include(z => z.PartNumber).FirstOrDefaultAsync(z => z.Id == line.ZipId);
            if (zip == null) return NotFound(new { message = "Запчасть не найдена" });

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{i}",
                CountOrdered = line.Count,
                ZipId = zip.Id,
                OrderDateTime = purchase.OrderDateTime,
                // Гостю не доверяем присланную цену — берём текущую цену запчасти на сервере.
                SellCost = zip.SellCost ?? 0m,
                OperationId = (short)OperationEnum.Sale,
                DeliveryStatusId = (short)DeliveryStatusEnum.created,
                PurchaseId = purchase.Id
            };
            db.Orders.Add(order);

            var incomeMoto = await db.IncomeMotos.FirstOrDefaultAsync(x => x.Id == zip.IncomeMotoId);

            var stock = await warehouse.ReserveForOrderAsync(
                zip, line.Count, order.Id, order.OrderNumber, order.SellCost, user.Id,
                $"Заказ {order.OrderNumber} для {user.PhoneNumber}:{user.FIO}. " +
                $"Деталь {zip.PartNumber.Name} взята с мотоцикла {incomeMoto?.Description ?? "—"}");

            if (stock.IsFailure) return stock.Error!.ToErrorResponse();

            items.Add(new OrderDto(
                order.Id, order.OrderNumber, order.CountOrdered, order.OrderDateTime,
                zip.PartNumber.Name, zip.IncomeCost, address.Address,
                order.SellCost, order.Discount, purchase.IsPaid, purchase.ReceiptFileName,
                purchase.Id, purchase.PurchaseNumber));
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new PurchaseDto(purchase.Id, purchase.PurchaseNumber, items));
    }
}

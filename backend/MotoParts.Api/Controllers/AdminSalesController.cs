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

/// <summary>Продажи: пользователи, адреса доставки и заказы.</summary>
public class AdminSalesController(AppDbContext db, WarehouseService warehouse) : AdminControllerBase
{
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

        // Создание пользователя с ролями — тот же путь повышения привилегий, что и правка:
        // без этой проверки регистратор просто заводил бы себе второй аккаунт с правами админа.
        if ((request.IsAdmin || request.IsRegistrar || request.IsSender) && !User.IsInRole("Admin"))
            return Forbid();

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
                o.Purchase.AddressId,
                o.OrderDateTime,
                o.SellCost,
                o.PriceCost,
                o.OperationId,
                o.Discount,
                o.DiscountPercent,
                o.Purchase.UserId,
                UserFio = o.Purchase.User.FIO,
                o.DeliveryStatusId,
                DeliveryStatus = o.DeliveryStatus != null ? o.DeliveryStatus.Description : null,
                // Общие на всю покупку — одинаковы у всех её позиций (см. Purchase).
                o.PurchaseId,
                PurchaseNumber = o.Purchase.PurchaseNumber,
                IsPaid = o.Purchase.IsPaid,
                DeliveryCompany = o.Purchase.DeliveryCompany,
                DeliveryComment = o.Purchase.DeliveryComment,
                ReceiptFileName = o.Purchase.ReceiptFileName
            })
            .ToListAsync());

    /// <summary>
    /// Заводит заказ из одной позиции — «корзина из одного товара», заведённая администратором.
    /// Для покупок из нескольких позиций клиент проходит через корзину на витрине
    /// (см. PurchasesController.Checkout); в админке своей корзины пока нет.
    /// </summary>
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

        // Комментарий заказа необязателен — если пусто, генерируем номер, как это уже
        // делает публичный OrdersController для гостевых/пользовательских заказов.
        var orderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
            ? "ORD-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            : request.OrderNumber.Trim();

        var orderDateTime = request.OrderDateTime.HasValue
            // С фронта приходит только календарная дата (input[type=date], без времени и зоны) —
            // ASP.NET достраивает её локальным смещением сервера, а Npgsql пишет timestamptz
            // только с Offset=0. ToUniversalTime() тут сдвинул бы саму дату (например, на день
            // назад), поэтому просто фиксируем выбранный день на полночь UTC, без конвертации.
            ? new DateTimeOffset(request.OrderDateTime.Value.Date, TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseNumber = "PUR-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UserId = request.UserId,
            AddressId = request.AddressId,
            OrderDateTime = orderDateTime,
            IsPaid = request.IsPaid,
            DeliveryCompany = request.DeliveryCompany,
            DeliveryComment = request.DeliveryComment
        };
        db.Purchases.Add(purchase);

        // Прайс-цена по умолчанию — текущая цена запчасти: именно её подставляет форма.
        var pricing = DiscountPolicy.FromSellCost(request.PriceCost ?? zip.SellCost, request.SellCost);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CountOrdered = request.CountOrdered,
            ZipId = request.ZipId,
            OrderDateTime = orderDateTime,
            // Скидку пересчитываем на сервере из прайс-цены, а не берём из запроса: присланная
            // тройка (цена, скидка, процент) могла быть несогласованной — правило одно (DiscountPolicy).
            SellCost = pricing.SellCost,
            PriceCost = pricing.PriceCost,
            Discount = pricing.Discount,
            DiscountPercent = pricing.DiscountPercent,
            OperationId = request.OperationId ?? (short)OperationEnum.Sale,
            // Форма в админке не даёт выбрать статус доставки — без дефолта заказ оставался
            // с DeliveryStatusId = null и не попадал ни в одну вкладку «Отправлений».
            DeliveryStatusId = request.DeliveryStatusId ?? (short)DeliveryStatusEnum.created,
            PurchaseId = purchase.Id
        };
        db.Orders.Add(order);

        // Списание со склада и строка журнала — одной операцией через WarehouseService.
        var stock = await warehouse.ReserveForOrderAsync(
            zip, request.CountOrdered, order.Id, order.OrderNumber, order.SellCost, CurrentUserId,
            $"Заказ {order.OrderNumber} заведён из админ-панели");

        if (stock.IsFailure) return stock.Error!.ToErrorResponse();

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { order.Id, order.OrderNumber });
    }

    /// <summary>
    /// Отдаёт PDF-этикетку с QR-кодом запчасти. Генерация PDF живёт только здесь (не в AddOrder) —
    /// печатают его явно, по кнопке в «Печать QR-кода», а не автоматически при каждом заказе.
    /// Печать доступна по любой заведённой запчасти — заказы на неё роли не играют.
    /// </summary>
    [HttpPut("addressess/{id:int}")]
    public async Task<IActionResult> UpdateAddress(int id, AdminAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { message = "Адрес обязателен" });

        var address = await db.DeliveryAddressess.FindAsync(id);
        if (address == null) return NotFound(new { message = "Адрес не найден" });

        if (request.UserId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.UserId))
            return BadRequest(new { message = "Пользователь не найден" });

        address.Address = request.Address.Trim();
        address.PostCode = request.PostCode;
        address.UserId = request.UserId;
        await db.SaveChangesAsync();
        return Ok(new { address.Id, address.Address, address.PostCode, address.UserId });
    }

    /// <summary>
    /// Правка заказа. Количество и запчасть здесь не меняются: они уже отражены в остатке и
    /// журнале, и тихая правка развела бы склад с историей. Чтобы изменить количество, заказ
    /// удаляют и заводят заново либо проводят коррекцию.
    ///
    /// Адрес, покупатель, оплата и доставка принадлежат не самому заказу, а покупке целиком
    /// (см. Purchase) — если заказ из покупки в несколько позиций, правка через любую из них
    /// обновит их все одинаково. Это осознанно: у одной покупки один адрес и одна оплата.
    /// </summary>
    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id, AdminOrderRequest request)
    {
        var order = await db.Orders.Include(o => o.Purchase).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Заказ не найден" });

        if (!await db.DeliveryAddressess.AnyAsync(a => a.Id == request.AddressId))
            return BadRequest(new { message = "Адрес доставки не найден" });
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId))
            return BadRequest(new { message = "Покупатель не найден" });

        if (!string.IsNullOrWhiteSpace(request.OrderNumber))
            order.OrderNumber = request.OrderNumber.Trim();

        order.Purchase.AddressId = request.AddressId;
        order.Purchase.UserId = request.UserId;
        if (request.OrderDateTime.HasValue)
            order.OrderDateTime = new DateTimeOffset(request.OrderDateTime.Value.Date, TimeSpan.Zero);

        // Скидки пересчитываются на сервере из прайс-цены — присланным значениям не доверяем.
        var pricing = DiscountPolicy.FromSellCost(request.PriceCost ?? order.PriceCost, request.SellCost);
        order.PriceCost = pricing.PriceCost;
        order.SellCost = pricing.SellCost;
        order.Discount = pricing.Discount;
        order.DiscountPercent = pricing.DiscountPercent;

        if (request.DeliveryStatusId.HasValue)
        {
            if (!await db.DeliveryStatuses.AnyAsync(s => s.Id == request.DeliveryStatusId.Value))
                return BadRequest(new { message = "Статус доставки не найден" });
            order.DeliveryStatusId = request.DeliveryStatusId;
        }

        order.Purchase.IsPaid = request.IsPaid;
        order.Purchase.DeliveryCompany = request.DeliveryCompany;
        order.Purchase.DeliveryComment = request.DeliveryComment;

        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.OrderNumber });
    }

    /// <summary>
    /// Правка пользователя. Роли и пароль меняет только администратор: иначе регистратор,
    /// у которого тоже есть доступ к этому контроллеру, мог бы выдать себе права админа.
    /// </summary>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, AdminUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FIO))
            return BadRequest(new { message = "Email и ФИО обязательны" });

        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Пользователь не найден" });

        var email = request.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        var rolesOrPasswordChanged = request.IsAdmin != user.IsAdmin
            || request.IsRegistrar != user.IsRegistrar
            || request.IsSender != user.IsSender
            || !string.IsNullOrEmpty(request.Password);

        if (rolesOrPasswordChanged && !User.IsInRole("Admin"))
            return Forbid();

        user.Email = email;
        user.FIO = request.FIO.Trim();
        user.PhoneNumber = request.PhoneNumber;

        if (rolesOrPasswordChanged)
        {
            user.IsAdmin = request.IsAdmin;
            user.IsRegistrar = request.IsRegistrar;
            user.IsSender = request.IsSender;
            if (!string.IsNullOrEmpty(request.Password))
                user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.FIO });
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

    /// <summary>Выдача ролей — операция администратора; регистратору она недоступна.</summary>
    [Authorize(Roles = "Admin")]
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

}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Application.Warehouse;
using MotoParts.Infrastructure.Services;

using System.Linq;
using System.Security.Claims;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(AppDbContext db, WarehouseService warehouse) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
                //o.Payment != null ? o.Payment.Status : null,
                o.SellCost, o.Discount))
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
            DeliveryStatusId = (short)DeliveryStatusEnum.created
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
            order.SellCost, order.Discount));
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
            DeliveryStatusId = (short)DeliveryStatusEnum.created
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
            order.Discount
        );

        return Ok(dto);
    }
}

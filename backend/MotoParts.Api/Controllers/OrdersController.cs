using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;
using MotoParts.Api.Services;

using System.Linq;
using System.Security.Claims;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(AppDbContext db) : ControllerBase
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
                o.Nomenclature != null ? o.Nomenclature.Name : null,
                o.Nomenclature != null ? o.Nomenclature.IncomeCost : null,
                o.Address.Address,
                //o.Payment != null ? o.Payment.Status : null,
                o.SellCost))
            .ToListAsync();

        return Ok(new PagedResult<OrderDto>(items, total, page, pageSize));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        var storedItem = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == request.ZipId);

        if (storedItem == null || storedItem.Count < request.Count)
        {
            return BadRequest(new { message = "Недостаточно товара на складе. Доступно: " + (storedItem?.Count ?? 0) });
        }

        storedItem.Count -= request.Count;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-" + DateTimeOffset.Now.ToUnixTimeSeconds(),
            CountOrdered = request.Count,
            NomenclatureId = request.ZipId,
            AddressId = request.AddressId,
            OrderDateTime = DateTimeOffset.UtcNow,
            DeliveryStatusId = (short)DeliveryStatusEnum.created
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return Ok(order);
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

    [HttpPost("guest-order")]
    public async Task<ActionResult<OrderDto>> CreateGuestOrder([FromBody] GuestCreateOrderRequest req)
    {
        // 1. Ищем запчасть
        var zip = await db.Zips.FindAsync(req.ZipId);
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
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            CountOrdered = req.Count,
            NomenclatureId = zip.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = zip.SellCost,
            Discount = req.Promo,
            UserId = user.Id
        };
        db.Orders.Add(order);
        var incomeMoto = await db.IncomeMotos.FirstOrDefaultAsync(x=>x.Id == zip.IncomeMotoId);
        // 5. Добавляем запись в таблицу Log
        var logEntry = new Log
        {
            Description = $"{DateTimeOffset.UtcNow} был создан заказ {order.OrderNumber} " +
                            $"для пользователя {user.PhoneNumber}:{user.FIO} " +
                            $"по цене {zip.SellCost} в количстве {order.CountOrdered}шт." +
                            $"Деталь {zip.Name} взята с мотоцикла {incomeMoto.Description}",
            OrderId = order.Id
        };
        db.Logs.Add(logEntry);

        await db.SaveChangesAsync();

        // Формируем ответ (аналогичный существующему методу создания)
        var dto = new OrderDto(
            order.Id,
            order.OrderNumber,
            order.CountOrdered,
            order.OrderDateTime,
            zip.Name,
            zip.IncomeCost,
            address.Address,
            zip.SellCost,
            order.Discount
        );

        return Ok(dto);
    }
}

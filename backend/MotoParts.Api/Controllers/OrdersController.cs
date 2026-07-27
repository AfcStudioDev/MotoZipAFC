using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;

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
            .Where(o => o.Adress.UserId == userId)
            .OrderByDescending(o => o.OrderDateTime);

        var total = await orders.CountAsync();
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id, o.OrderNumber, o.CountOrdered, o.OrderDateTime,
                o.Nomenclature != null ? o.Nomenclature.Name : null,
                o.Nomenclature != null ? o.Nomenclature.IncomeCost : null,
                o.Adress.Adress,
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
            AdressId = request.AdressId,
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
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Infrastructure.Persistence;
using MotoParts.Application.Contracts;

using System.Security.Claims;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Просмотр уже оформленных заказов (позиций покупок) текущего пользователя.
/// Само оформление — см. PurchasesController.
/// </summary>
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
            .Where(o => o.Purchase.UserId == userId)
            .OrderByDescending(o => o.OrderDateTime);

        var total = await orders.CountAsync();
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id, o.OrderNumber, o.CountOrdered, o.OrderDateTime,
                o.Zip.PartNumber.Name,
                o.Zip.IncomeCost,
                o.Purchase.Address.Address,
                o.SellCost, o.Discount, o.Purchase.IsPaid, o.Purchase.ReceiptFileName,
                o.PurchaseId, o.Purchase.PurchaseNumber))
            .ToListAsync();

        return Ok(new PagedResult<OrderDto>(items, total, page, pageSize));
    }

    [HttpDelete("orders/{id}")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Заказ не найден" });

        db.Orders.Remove(order);
        await db.SaveChangesAsync();

        return Ok(new { message = "Заказ успешно удален" });
    }
}

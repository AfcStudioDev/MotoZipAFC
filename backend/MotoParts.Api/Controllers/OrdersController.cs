using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;

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
                o.Nomenclature != null ? o.Nomenclature.Cost : null,
                o.Address.Address,
                o.Payment != null ? o.Payment.Status : null))
            .ToListAsync();

        return Ok(new PagedResult<OrderDto>(items, total, page, pageSize));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        if (request.Count <= 0)
            return BadRequest(new { message = "Количество должно быть больше нуля" });

        var userId = CurrentUserId;
        var address = await db.DeliveryAddresses
            .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == userId);
        if (address is null)
            return BadRequest(new { message = "Адрес доставки не найден" });

        var zip = await db.Zip.FindAsync(request.ZipId);
        if (zip is null)
            return NotFound(new { message = "Запчасть не найдена" });
        if (zip.CountStored < request.Count)
            return BadRequest(new { message = $"На складе только {zip.CountStored} шт." });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}",
            CountOrdered = request.Count,
            NomenclatureId = zip.Id,
            AddressId = address.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
        };
        zip.CountStored -= request.Count;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return Ok(new OrderDto(
            order.Id, order.OrderNumber, order.CountOrdered, order.OrderDateTime,
            zip.Name, zip.Cost, address.Address, null));
    }
}

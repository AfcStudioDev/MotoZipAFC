using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Data;

namespace MotoParts.Api.Controllers
{
    [ApiController]
    [Route("api/sender")]
    // [Authorize] // Раскомментируйте и добавьте проверку роли IsSender через Policy, если настроено
    public class SenderController(AppDbContext db) : ControllerBase
    {
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            // В реальном проекте добавьте проверку: if (!user.IsSender) return Forbid();

            var orders = await db.Orders
                .Include(o => o.Nomenclature)
                .Include(o => o.Address)
                .OrderByDescending(o => o.OrderDateTime)
                .Select(o => new {
                    o.Id,
                    o.OrderNumber,
                    o.CountOrdered,
                    o.DeliveryStatus,
                    o.OrderDateTime,
                    ZipName = o.Nomenclature.Name,
                    Address = o.Address.Adress
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto request)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // Если заказ отменяют - возвращаем товар на склад
            if (request.Status == "canceled" && order.DeliveryStatus != "canceled")
            {
                var storedItem = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == order.NomenclatureId);
                if (storedItem != null)
                {
                    storedItem.Count += order.CountOrdered;
                }
                else if (order.NomenclatureId.HasValue)
                {
                    db.Stored.Add(new Models.Stored { ZipId = order.NomenclatureId.Value, Count = order.CountOrdered });
                }
            }
            // Если заказ восстанавливают из отмененных - нужно снова списать (здесь упрощено)

            order.DeliveryStatus = request.Status;
            await db.SaveChangesAsync();

            return Ok();
        }
    }

    public class UpdateStatusDto { public string Status { get; set; } = null!; }
}

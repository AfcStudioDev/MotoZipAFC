using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Data;
using MotoParts.Api.Models;

namespace MotoParts.Api.Controllers
{
    [ApiController]
    [Route("api/sender")]
    [Authorize(Roles = "Admin,Sender")] // Защищаем эндпоинты авторизацией
    public class SenderController(AppDbContext db) : ControllerBase
    {
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await db.Orders
                .Include(o => o.Zip).ThenInclude(z => z.PartNumber)
                .Include(o => o.Address) // Исправлено с Address на Address
                .Include(o => o.DeliveryStatus) // Подтягиваем новый справочник статусов
                .OrderByDescending(o => o.OrderDateTime)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.CountOrdered,
                    // Для фронтенда отдаем текстовое описание статуса (например, "created", "sent")
                    DeliveryStatus = o.DeliveryStatus != null ? o.DeliveryStatus.Description : "unknown",
                    o.OrderDateTime,
                    ZipName = o.Zip.PartNumber.Name,
                    PartNum = o.Zip.PartNumber.PartNum,
                    Address = o.Address.Address // Исправлено с Address.Address на Address.Address
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto request)
        {
            // Находим заказ и его текущий статус
            var order = await db.Orders
                .Include(o => o.DeliveryStatus)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound(new { message = "Заказ не найден" });

            // Ищем новый статус в БД по строке, пришедшей с фронтенда (например, "canceled")
            var newStatus = await db.DeliveryStatuses
                .FirstOrDefaultAsync(s => s.Description == request.Status.ToLower());

            if (newStatus == null)
                return BadRequest(new { message = "Неизвестный статус доставки" });

            var oldStatusDescription = order.DeliveryStatus?.Description;

            // Если статус не изменился, просто возвращаем Ok
            if (order.DeliveryStatusId == newStatus.Id)
                return Ok(new { message = "Статус уже установлен" });

            // Остаток и журнал меняются в одной транзакции, иначе они разъедутся при сбое.
            await using var tx = await db.Database.BeginTransactionAsync();

            // --- ЛОГИКА СКЛАДА ---
            // Если заказ отменяют - возвращаем товар на склад
            if (newStatus.Description == "canceled" && oldStatusDescription != "canceled")
            {
                var storedItem = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == order.ZipId);
                if (storedItem != null)
                {
                    storedItem.Count += order.CountOrdered;
                }
                else
                {
                    db.Stored.Add(new Stored { ZipId = order.ZipId, Count = order.CountOrdered });
                }

                db.Logs.Add(new Log
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    OperationId = (short)OperationEnum.Refund,
                    OrderId = order.Id,
                    ZipId = order.ZipId,
                    Qty = order.CountOrdered,
                    SellCost = order.SellCost,
                    Description = $"Возврат на склад: заказ {order.OrderNumber} отменён"
                });
            }
            // Если заказ восстанавливают из отмененных - нужно снова списать товар со склада
            else if (oldStatusDescription == "canceled" && newStatus.Description != "canceled")
            {
                var storedItem = await db.Stored.FirstOrDefaultAsync(s => s.ZipId == order.ZipId);
                if (storedItem != null)
                {
                    storedItem.Count = Math.Max(0, storedItem.Count - order.CountOrdered);
                }

                db.Logs.Add(new Log
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    OperationId = (short)OperationEnum.Sale,
                    OrderId = order.Id,
                    ZipId = order.ZipId,
                    Qty = -order.CountOrdered,
                    SellCost = order.SellCost,
                    Description = $"Повторное списание: заказ {order.OrderNumber} восстановлен из отменённых"
                });
            }

            // --- ОБНОВЛЕНИЕ СТАТУСА ---
            order.DeliveryStatusId = newStatus.Id;

            // --- ЛОГИРОВАНИЕ ---
            // Событие аудита: движения товара нет, поэтому Qty остаётся null.
            db.Logs.Add(new Log
            {
                CreatedAt = DateTimeOffset.UtcNow,
                OperationId = (short)OperationEnum.Other,
                OrderId = order.Id,
                ZipId = order.ZipId,
                Description = $"Статус доставки изменен с '{oldStatusDescription ?? "нет"}' на '{newStatus.Description}'"
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { message = "Статус успешно обновлен" });
        }

        // Отключено, т.к. сазали пока нет необходимости в этой фиче
        //[HttpDelete("orders/{id}")]
        //public async Task<IActionResult> DeleteOrder(Guid id)
        //{
        //    var order = await db.Orders.FindAsync(id);
        //    if (order == null) return NotFound();

        //    db.Orders.Remove(order);

        //    try
        //    {
        //        await db.SaveChangesAsync();
        //        return Ok(new { message = "Заказ успешно удален" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { message = "Не удалось удалить заказ", error = ex.Message });
        //    }
        //}
    }

    public class UpdateStatusDto { public string Status { get; set; } = null!; }
}

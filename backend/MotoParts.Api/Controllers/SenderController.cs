using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Domain.Models;
using MotoParts.Application.Warehouse;
using MotoParts.Infrastructure.Services;

using System.Security.Claims;

namespace MotoParts.Api.Controllers
{
    [ApiController]
    [Route("api/sender")]
    [Authorize(Roles = "Admin,Sender")] // Защищаем эндпоинты авторизацией
    public class SenderController(AppDbContext db, WarehouseService warehouse) : ControllerBase
    {
        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

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
                    Address = o.Address.Address, // Исправлено с Address.Address на Address.Address
                    o.IsPaid
                })
                .ToListAsync();

            return Ok(orders);
        }

        /// <summary>
        /// Проверка товара по GUID запчасти (сканирование/ввод перед отправкой) —
        /// отдаёт минимум для визуальной сверки: название, парт-номер, фото, донора.
        /// </summary>
        [HttpGet("zip/{id:guid}")]
        public async Task<IActionResult> GetZipInfo(Guid id)
        {
            var zip = await db.Zips
                .Where(z => z.Id == id)
                .Select(z => new
                {
                    z.Id,
                    Name = z.PartNumber.Name,
                    PartNum = z.PartNumber.PartNum,
                    IncomeMoto = z.IncomeMoto != null ? z.IncomeMoto.Description : null,
                    Photos = z.Photos.Select(p => p.FileName).ToList()
                })
                .FirstOrDefaultAsync();

            if (zip == null) return NotFound(new { message = "Запчасть с таким GUID не найдена" });

            return Ok(zip);
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

            // Пока оплата не подтверждена администратором вручную (онлайн-оплата отключена,
            // см. PaymentsController), заказ нельзя продвинуть дальше — только отменить
            // или вернуть в «не отправлено». Иначе отправитель мог бы отгрузить неоплаченное.
            if (!order.IsPaid && (newStatus.Description == "sent" || newStatus.Description == "completed"))
                return BadRequest(new { message = "Заказ не оплачен: доступны только отмена или возврат в «не отправлено»" });

            var oldStatusDescription = order.DeliveryStatus?.Description;

            // Если статус не изменился, просто возвращаем Ok
            if (order.DeliveryStatusId == newStatus.Id)
                return Ok(new { message = "Статус уже установлен" });

            // Остаток и журнал меняются в одной транзакции, иначе они разъедутся при сбое.
            await using var tx = await db.Database.BeginTransactionAsync();

            // --- ЛОГИКА СКЛАДА ---
            // Если заказ отменяют — возвращаем товар на склад
            if (newStatus.Description == "canceled" && oldStatusDescription != "canceled")
            {
                var stock = await warehouse.ReturnFromOrderAsync(
                    order.ZipId, order.CountOrdered, order.Id, order.SellCost, CurrentUserId,
                    $"Возврат на склад: заказ {order.OrderNumber} отменён");

                if (stock.IsFailure) return stock.Error!.ToErrorResponse();
            }
            // Если заказ восстанавливают из отменённых — снова списываем товар со склада
            else if (oldStatusDescription == "canceled" && newStatus.Description != "canceled")
            {
                var stock = await warehouse.ReserveAgainAsync(
                    order.ZipId, order.CountOrdered, order.Id, order.SellCost, CurrentUserId,
                    $"Повторное списание: заказ {order.OrderNumber} восстановлен из отменённых");

                if (stock.IsFailure) return stock.Error!.ToErrorResponse();
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

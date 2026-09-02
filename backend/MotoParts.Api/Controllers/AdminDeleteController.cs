using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Common;
using MotoParts.Application.Contracts;
using MotoParts.Application.Warehouse;
using MotoParts.Domain.Common;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Infrastructure.Services;

using PdfGeneration.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace MotoParts.Api.Controllers;

/// <summary>Удаление записей из таблиц админ-панели вместе со связями. Только администратору.</summary>
[AdminOnly]
public class AdminDeleteController(AppDbContext db, WarehouseService warehouse) : AdminControllerBase
{
    [HttpDelete("{endpoint}/{id}")]
    public async Task<IActionResult> DeleteEntity(string endpoint, string id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        switch (endpoint.ToLower())
        {
            case "marks":
            {
                if (!int.TryParse(id, out var markId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var mark = await db.MotoMarks.FindAsync(markId);
                if (mark == null) return NotFound();

                // Модели остаются, но теряют привязку к марке (MarkId допускает null).
                await db.MotoModels.Where(m => m.MarkId == markId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.MarkId, (int?)null));

                db.MotoMarks.Remove(mark);
                break;
            }

            case "models":
            {
                if (!int.TryParse(id, out var modelId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var model = await db.MotoModels.FindAsync(modelId);
                if (model == null) return NotFound();

                // Применимость к удаляемой модели смысла не имеет.
                await db.PartNumberApplicabilities.Where(a => a.ModelId == modelId).ExecuteDeleteAsync();

                db.MotoModels.Remove(model);
                break;
            }

            case "groups":
            {
                if (!int.TryParse(id, out var groupId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var group = await db.ZipGroups.FindAsync(groupId);
                if (group == null) return NotFound();

                // Каталожные позиции остаются, просто без группы.
                await db.PartNumbers.Where(p => p.GroupId == groupId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.GroupId, (int?)null));

                db.ZipGroups.Remove(group);
                break;
            }

            case "partnumbers":
            case "part-numbers":
            {
                if (!int.TryParse(id, out var partNumId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var pn = await db.PartNumbers.FindAsync(partNumId);
                if (pn == null) return NotFound();

                var zipCount = await db.Zips.CountAsync(z => z.PartNumId == partNumId);
                if (zipCount > 0)
                    return Conflict(new { message = $"По этому парт-номеру заведено запчастей: {zipCount}. Сначала удалите их." });

                await db.PartNumberApplicabilities.Where(a => a.PartNumId == partNumId).ExecuteDeleteAsync();
                await db.PartNumberSeriesApplicabilities.Where(a => a.PartNumId == partNumId).ExecuteDeleteAsync();

                db.PartNumbers.Remove(pn);
                break;
            }

            case "applicability":
            {
                if (!int.TryParse(id, out var linkId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var link = await db.PartNumberApplicabilities.FindAsync(linkId);
                if (link == null) return NotFound();
                db.PartNumberApplicabilities.Remove(link);
                break;
            }

            case "series":
            {
                if (!Guid.TryParse(id, out var seriesGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var series = await db.MotoSeries.FindAsync(seriesGuid);
                if (series == null) return NotFound();

                // Применимость к удаляемой серии смысла не имеет.
                await db.PartNumberSeriesApplicabilities.Where(a => a.SeriesId == seriesGuid).ExecuteDeleteAsync();

                db.MotoSeries.Remove(series);
                break;
            }

            case "series-applicability":
            {
                if (!int.TryParse(id, out var seriesLinkId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var seriesLink = await db.PartNumberSeriesApplicabilities.FindAsync(seriesLinkId);
                if (seriesLink == null) return NotFound();
                db.PartNumberSeriesApplicabilities.Remove(seriesLink);
                break;
            }

            case "zip":
            {
                if (!Guid.TryParse(id, out var zipGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var zip = await db.Zips.FindAsync(zipGuid);
                if (zip == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.ZipId == zipGuid);
                if (orderCount > 0)
                    return Conflict(new { message = $"На эту запчасть ссылаются заказы: {orderCount}. Удалить нельзя — иначе из истории продаж пропадёт документ." });

                // Файлы фотографий удаляем с диска до того, как исчезнут строки в БД.
                var photoNames = await db.ZipPhotos.Where(p => p.ZipId == zipGuid).Select(p => p.FileName).ToListAsync();
                var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ZipPhotos");
                foreach (var name in photoNames)
                {
                    var filePath = Path.Combine(uploadFolder, name);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }

                await db.ZipPhotos.Where(p => p.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.PriceHistories.Where(p => p.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.Logs.Where(l => l.ZipId == zipGuid).ExecuteDeleteAsync();
                await db.Stored.Where(s => s.ZipId == zipGuid).ExecuteDeleteAsync();

                db.Zips.Remove(zip);
                break;
            }

            case "users":
            {
                if (!int.TryParse(id, out var userId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var user = await db.Users.FindAsync(userId);
                if (user == null) return NotFound();

                var orderCount = await db.Purchases.CountAsync(p => p.UserId == userId);
                if (orderCount > 0)
                    return Conflict(new { message = $"У пользователя есть заказы: {orderCount}. Удалить нельзя — вместе с ним пропали бы документы." });

                // PriceHistory.UserId не допускает null, поэтому пользователя,
                // проводившего переоценку, удалить нельзя без потери истории цен.
                var repriceCount = await db.PriceHistories.CountAsync(p => p.UserId == userId);
                if (repriceCount > 0)
                    return Conflict(new { message = $"Пользователь проводил переоценку ({repriceCount} записей в истории цен). Удалить нельзя." });

                // В журнале и у доноров автор остаётся неизвестным — поля допускают null.
                await db.Logs.Where(l => l.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.UserId, (int?)null));
                await db.IncomeMotos.Where(i => i.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.UserId, (int?)null));

                await db.DeliveryAddressess.Where(a => a.UserId == userId).ExecuteDeleteAsync();

                db.Users.Remove(user);
                break;
            }

            case "addressess":
            {
                if (!int.TryParse(id, out var addressId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var address = await db.DeliveryAddressess.FindAsync(addressId);
                if (address == null) return NotFound();

                var orderCount = await db.Purchases.CountAsync(p => p.AddressId == addressId);
                if (orderCount > 0)
                    return Conflict(new { message = $"На этот адрес оформлены заказы: {orderCount}. Удалить нельзя." });

                db.DeliveryAddressess.Remove(address);
                break;
            }

            case "orders":
            {
                if (!Guid.TryParse(id, out var orderGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var order = await db.Orders.Include(o => o.DeliveryStatus).FirstOrDefaultAsync(o => o.Id == orderGuid);
                if (order == null) return NotFound();

                var statusDescription = order.DeliveryStatus?.Description;

                // "canceled" уже вернул остаток на склад при смене статуса (см. SenderController.UpdateStatus) —
                // повторный возврат задвоил бы количество. "completed" — товар реально отгружен покупателю,
                // удаление документа задним числом не должно магически возвращать его на склад.
                // Во всех остальных случаях (null, created, sent) заказ ещё не завершён — резерв возвращаем.
                if (statusDescription != "completed" && statusDescription != "canceled")
                {
                    // orderId не передаём: заказ сейчас исчезнет, ссылаться будет не на что —
                    // номер сохраняется в описании.
                    var stock = await warehouse.ReturnFromOrderAsync(
                        order.ZipId, order.CountOrdered, orderId: null, order.SellCost, CurrentUserId,
                        $"Возврат на склад: незавершённый заказ {order.OrderNumber} удалён из админ-панели");

                    if (stock.IsFailure) return stock.Error!.ToErrorResponse();
                }

                // Журнал — история движения товара, а не приложение к заказу: строку продажи
                // затирать нельзя, иначе в «Истории по детали» продажа подменялась бы возвратом.
                // Поэтому вместо удаления просто снимаем ссылку на исчезающий заказ (OrderId
                // допускает null, FK стоит на Restrict) — записи остаются, привязка к детали
                // через ZipId сохраняется, а номер заказа виден в Description.
                await db.Logs.Where(l => l.OrderId == orderGuid)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.OrderId, (Guid?)null));

                var purchaseId = order.PurchaseId;
                db.Orders.Remove(order);

                // Purchase хранит адрес/оплату/доставку отдельно от своих позиций (Order) — удаление
                // последней позиции покупки раньше оставляло пустую Purchase висеть в базе: в
                // «Заказах» она уже не видна ни одной строкой, но всё ещё ссылается на адрес и
                // блокирует его удаление ("На этот адрес оформлены заказы: N"), хотя админ этого
                // заказа уже не видит нигде.
                var remainingOrders = await db.Orders.CountAsync(o => o.PurchaseId == purchaseId && o.Id != orderGuid);
                if (remainingOrders == 0)
                {
                    var purchase = await db.Purchases.FindAsync(purchaseId);
                    if (purchase != null)
                    {
                        if (!string.IsNullOrEmpty(purchase.ReceiptFileName))
                        {
                            var receiptPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Receipts", purchase.ReceiptFileName);
                            if (System.IO.File.Exists(receiptPath)) System.IO.File.Delete(receiptPath);
                        }
                        db.Purchases.Remove(purchase);
                    }
                }

                break;
            }

            case "incomemotos":
            {
                if (!Guid.TryParse(id, out var donorGuid)) return BadRequest(new { message = "Неверный формат GUID" });
                var donor = await db.IncomeMotos.FindAsync(donorGuid);
                if (donor == null) return NotFound();

                var zipCount = await db.Zips.CountAsync(z => z.IncomeMotoId == donorGuid);
                if (zipCount > 0)
                    return Conflict(new { message = $"С этого донора заведено запчастей: {zipCount}. Сначала удалите их." });

                db.IncomeMotos.Remove(donor);
                break;
            }

            case "logs":
            {
                if (!int.TryParse(id, out var logId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var log = await db.Logs.FindAsync(logId);
                if (log == null) return NotFound();
                db.Logs.Remove(log);
                break;
            }

            case "deliverystatuses":
            {
                if (!short.TryParse(id, out var statusId)) return BadRequest(new { message = "Неверный формат идентификатора" });
                var ds = await db.DeliveryStatuses.FindAsync(statusId);
                if (ds == null) return NotFound();

                var orderCount = await db.Orders.CountAsync(o => o.DeliveryStatusId == statusId);
                if (orderCount > 0)
                    return Conflict(new { message = $"Статус используется в заказах: {orderCount}. Удалить нельзя." });

                db.DeliveryStatuses.Remove(ds);
                break;
            }

            default:
                return BadRequest(new { message = "Неизвестный эндпоинт" });
        }

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { message = "Запись успешно удалена" });
        }
        catch (DbUpdateException)
        {
            // Нарушение внешнего ключа — единственная ожидаемая здесь ошибка, её и объясняем.
            // Текст исключения наружу больше не отдаём: в нём были имена таблиц и ограничений.
            await tx.RollbackAsync();
            return Error.Conflict("Невозможно удалить запись: на неё ссылаются другие данные").ToErrorResponse();
        }
    }
}

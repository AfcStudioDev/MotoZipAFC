using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;
using MotoParts.Api.Services;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(AppDbContext db, YooKassaService yooKassa, ILogger<PaymentsController> logger)
    : ControllerBase
{
    /// <summary>Создать платёж ЮKassa (банковская карта РФ) для заказа текущего пользователя.</summary>
    //[HttpPost("create")]
    //[Authorize]
    //public async Task<ActionResult<CreatePaymentResponse>> Create(CreatePaymentRequest request)
    //{
    //    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    //    var order = await db.Orders
    //        .Include(o => o.Nomenclature)
    //        .Include(o => o.Payment)
    //        .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.Address.UserId == userId);
    //    if (order is null)
    //        return NotFound(new { message = "Заказ не найден" });
    //    if (order.Payment?.Status == "succeeded")
    //        return BadRequest(new { message = "Заказ уже оплачен" });
    //    if (order.Nomenclature is null)
    //        return BadRequest(new { message = "В заказе нет номенклатуры" });

    //    var amount = order.Nomenclature.IncomeCost * order.CountOrdered;
    //    var payment = await yooKassa.CreatePaymentAsync(
    //        amount, $"Оплата заказа {order.OrderNumber}", request.ReturnUrl, order.Id);

    //    if (order.Payment is null)
    //    {
    //        db.Payments.Add(new Payment
    //        {
    //            Id = Guid.NewGuid(),
    //            OrderId = order.Id,
    //            YooKassaPaymentId = payment.Id,
    //            Status = payment.Status,
    //            Amount = amount,
    //            CreatedAt = DateTimeOffset.UtcNow,
    //        });
    //    }
    //    else
    //    {
    //        order.Payment.YooKassaPaymentId = payment.Id;
    //        order.Payment.Status = payment.Status;
    //        order.Payment.Amount = amount;
    //    }

    //    await db.SaveChangesAsync();

    //    return Ok(new CreatePaymentResponse(payment.Id, payment.ConfirmationUrl!));
    //}

    ///// <summary>Проверить статус платежа (используется страницей возврата после оплаты).</summary>
    //[HttpGet("status/{orderId:guid}")]
    //[Authorize]
    //public async Task<IActionResult> Status(Guid orderId)
    //{
    //    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    //    var payment = await db.Payments
    //        .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Order.Address.UserId == userId);
    //    if (payment is null)
    //        return NotFound(new { message = "Платёж не найден" });

    //    // Сверяем актуальный статус с ЮKassa
    //    var actual = await yooKassa.GetPaymentAsync(payment.YooKassaPaymentId);
    //    if (actual.Status != payment.Status)
    //    {
    //        payment.Status = actual.Status;
    //        await db.SaveChangesAsync();
    //    }

    //    return Ok(new { orderId, status = payment.Status });
    //}

    ///// <summary>
    ///// Webhook уведомлений ЮKassa (настраивается в личном кабинете магазина).
    ///// Статус подтверждается обратным запросом к API — тело уведомления не является доверенным.
    ///// </summary>
    //[HttpPost("webhook")]
    //[AllowAnonymous]
    //public async Task<IActionResult> Webhook()
    //{
    //    using var reader = new StreamReader(Request.Body);
    //    var body = await reader.ReadToEndAsync();
    //    logger.LogInformation("ЮKassa webhook: {Body}", body);

    //    try
    //    {
    //        using var doc = JsonDocument.Parse(body);
    //        var paymentId = doc.RootElement.GetProperty("object").GetProperty("id").GetString();
    //        if (paymentId is null) return Ok();

    //        var payment = await db.Payments.FirstOrDefaultAsync(p => p.YooKassaPaymentId == paymentId);
    //        if (payment is not null)
    //        {
    //            var actual = await yooKassa.GetPaymentAsync(paymentId);
    //            payment.Status = actual.Status;
    //            await db.SaveChangesAsync();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogWarning(ex, "Не удалось обработать webhook ЮKassa");
    //    }

    //    return Ok();
    //}
}

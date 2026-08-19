using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

using System.Security.Claims;

using VkChatBot;

namespace MotoParts.Api.Controllers;

/// <summary>Обращения в поддержку от лица покупателя: список своих обращений, создание, переписка.</summary>
[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController(AppDbContext db, IVkBotService vkBot) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<SupportTicketSummaryDto>>> My()
    {
        var userId = CurrentUserId;

        var tickets = await db.SupportTickets
            .Where(t => t.UserId == userId)
            .Include(t => t.Order)
            .Include(t => t.Messages)
            .OrderByDescending(t => t.Messages.Max(m => m.CreatedAt))
            .ToListAsync();

        return Ok(tickets.Select(ToSummary).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupportTicketDto>> Get(Guid id)
    {
        var ticket = await db.SupportTickets
            .Include(t => t.Order)
            .Include(t => t.Messages).ThenInclude(m => m.AuthorUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);

        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });

        return Ok(ToDto(ticket));
    }

    [HttpPost]
    public async Task<ActionResult<SupportTicketDto>> Create(CreateSupportTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Опишите ваше обращение" });

        var userId = CurrentUserId;

        // Заказ должен принадлежать текущему пользователю — иначе можно было бы
        // открывать обращения по чужим заказам, зная только их Id.
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId && o.Purchase.UserId == userId);
        if (order == null) return NotFound(new { message = "Заказ не найден" });

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrderId = order.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ticket.Messages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = userId,
            IsFromAdmin = false,
            Text = request.Message.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        vkBot.SendMessage(
            $"💬 Новое обращение в поддержку по заказу {order.OrderNumber}\n{request.Message.Trim()}");

        ticket = await db.SupportTickets
            .Include(t => t.Order)
            .Include(t => t.Messages).ThenInclude(m => m.AuthorUser)
            .FirstAsync(t => t.Id == ticket.Id);

        return Ok(ToDto(ticket));
    }

    [HttpPost("{id}/messages")]
    public async Task<ActionResult<SupportMessageDto>> SendMessage(Guid id, SendSupportMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Сообщение не может быть пустым" });

        var ticket = await db.SupportTickets
            .Include(t => t.Order)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });
        if (ticket.IsClosed) return BadRequest(new { message = "Обращение закрыто" });

        var message = new SupportMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = CurrentUserId,
            IsFromAdmin = false,
            Text = request.Text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SupportMessages.Add(message);
        await db.SaveChangesAsync();

        vkBot.SendMessage(
            $"💬 Ответ по обращению (заказ {ticket.Order.OrderNumber})\n{message.Text}");

        await db.Entry(message).Reference(m => m.AuthorUser).LoadAsync();
        return Ok(ToMessageDto(message));
    }

    private static SupportTicketSummaryDto ToSummary(SupportTicket t)
    {
        var last = t.Messages.OrderByDescending(m => m.CreatedAt).First();
        return new SupportTicketSummaryDto(
            t.Id, t.Order.OrderNumber, t.CreatedAt,
            last.CreatedAt, last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text, t.IsClosed);
    }

    private static SupportTicketDto ToDto(SupportTicket t) => new(
        t.Id, t.Order.OrderNumber, t.OrderId, t.CreatedAt, t.IsClosed,
        t.Messages.OrderBy(m => m.CreatedAt).Select(ToMessageDto).ToList());

    private static SupportMessageDto ToMessageDto(SupportMessage m) => new(
        m.Id, m.IsFromAdmin, m.AuthorUser.FIO, m.Text, m.CreatedAt);
}

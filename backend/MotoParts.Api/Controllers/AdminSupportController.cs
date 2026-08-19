using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Infrastructure.Persistence;

namespace MotoParts.Api.Controllers;

/// <summary>Все обращения в поддержку — видны администратору и регистратору (роли базового класса).</summary>
public class AdminSupportController(AppDbContext db) : AdminControllerBase
{
    [HttpGet("support")]
    public async Task<ActionResult<List<SupportTicketSummaryDto>>> List()
    {
        var tickets = await db.SupportTickets
            .Include(t => t.Order)
            .Include(t => t.User)
            .Include(t => t.Messages)
            .OrderByDescending(t => t.Messages.Max(m => m.CreatedAt))
            .ToListAsync();

        return Ok(tickets.Select(t =>
        {
            var last = t.Messages.OrderByDescending(m => m.CreatedAt).First();
            return new SupportTicketSummaryDto(
                t.Id, t.Order.OrderNumber, t.CreatedAt,
                last.CreatedAt, last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text, t.IsClosed,
                t.User.FIO, t.User.Email);
        }).ToList());
    }

    [HttpGet("support/{id}")]
    public async Task<ActionResult<SupportTicketDto>> Get(Guid id)
    {
        var ticket = await db.SupportTickets
            .Include(t => t.Order)
            .Include(t => t.Messages).ThenInclude(m => m.AuthorUser)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });

        return Ok(new SupportTicketDto(
            ticket.Id, ticket.Order.OrderNumber, ticket.OrderId, ticket.CreatedAt, ticket.IsClosed,
            ticket.Messages.OrderBy(m => m.CreatedAt)
                .Select(m => new SupportMessageDto(m.Id, m.IsFromAdmin, m.AuthorUser.FIO, m.Text, m.CreatedAt))
                .ToList()));
    }

    [HttpPost("support/{id}/messages")]
    public async Task<ActionResult<SupportMessageDto>> Reply(Guid id, SendSupportMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Сообщение не может быть пустым" });

        var ticket = await db.SupportTickets.Include(t => t.Order).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });
        if (ticket.IsClosed) return BadRequest(new { message = "Обращение закрыто" });

        var message = new SupportMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = CurrentUserId!.Value,
            IsFromAdmin = true,
            Text = request.Text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SupportMessages.Add(message);
        await db.SaveChangesAsync();

        await db.Entry(message).Reference(m => m.AuthorUser).LoadAsync();
        return Ok(new SupportMessageDto(message.Id, message.IsFromAdmin, message.AuthorUser.FIO, message.Text, message.CreatedAt));
    }

    [HttpPost("support/{id}/close")]
    public async Task<ActionResult<SupportTicketSummaryDto>> Close(Guid id)
    {
        var ticket = await db.SupportTickets
            .Include(t => t.Order)
            .Include(t => t.User)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });
        if (ticket.IsClosed) return BadRequest(new { message = "Обращение уже закрыто" });

        ticket.IsClosed = true;
        ticket.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var last = ticket.Messages.OrderByDescending(m => m.CreatedAt).First();
        return Ok(new SupportTicketSummaryDto(
            ticket.Id, ticket.Order.OrderNumber, ticket.CreatedAt,
            last.CreatedAt, last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text, ticket.IsClosed,
            ticket.User.FIO, ticket.User.Email));
    }

    [HttpDelete("support/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound(new { message = "Обращение не найдено" });

        // Переписка удалится каскадно (см. SupportMessageConfiguration).
        db.SupportTickets.Remove(ticket);
        await db.SaveChangesAsync();

        return Ok(new { message = "Обращение удалено" });
    }
}

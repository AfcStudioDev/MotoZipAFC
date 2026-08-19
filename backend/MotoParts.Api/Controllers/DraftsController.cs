using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MotoParts.Infrastructure.Persistence;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;

using System.Security.Claims;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Черновики форм админ-панели. Хранятся на сервере, а не в localStorage, чтобы незавершённый
/// ввод переживал очистку браузера и открывался с другого устройства. Черновик всегда личный:
/// и чтение, и запись идут только по id текущего пользователя из токена.
/// </summary>
[ApiController]
[Route("api/drafts")]
[Authorize]
public class DraftsController(AppDbContext db) : ControllerBase
{
    /// <summary>Формы, для которых черновики вообще разрешены — чтобы эндпоинт не стал произвольным хранилищем.</summary>
    private static readonly string[] AllowedFormKeys = ["orders", "zip"];

    /// <summary>Ограничение на размер черновика: это значения полей формы, а не файлы.</summary>
    private const int MaxContentLength = 20_000;

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("{formKey}")]
    public async Task<IActionResult> Get(string formKey)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });
        if (!AllowedFormKeys.Contains(formKey)) return BadRequest(new { message = "Неизвестная форма" });

        var draft = await db.UserDrafts
            .Where(d => d.UserId == userId.Value && d.FormKey == formKey)
            .Select(d => new { d.FormKey, d.Content, d.UpdatedAt })
            .FirstOrDefaultAsync();

        // Отсутствие черновика — штатная ситуация, поэтому 200 с null, а не 404:
        // фронтенду не нужно отличать «нет черновика» от ошибки.
        return Ok(draft);
    }

    [HttpPut("{formKey}")]
    public async Task<IActionResult> Save(string formKey, [FromBody] SaveDraftRequest request)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });
        if (!AllowedFormKeys.Contains(formKey)) return BadRequest(new { message = "Неизвестная форма" });

        var content = request.Content ?? "";
        if (content.Length > MaxContentLength)
            return BadRequest(new { message = "Черновик слишком большой" });

        var draft = await db.UserDrafts
            .FirstOrDefaultAsync(d => d.UserId == userId.Value && d.FormKey == formKey);

        if (draft is null)
        {
            db.UserDrafts.Add(new UserDraft
            {
                UserId = userId.Value,
                FormKey = formKey,
                Content = content,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            draft.Content = content;
            draft.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Черновик сохранён" });
    }

    [HttpDelete("{formKey}")]
    public async Task<IActionResult> Delete(string formKey)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(new { message = "Не удалось определить пользователя" });
        if (!AllowedFormKeys.Contains(formKey)) return BadRequest(new { message = "Неизвестная форма" });

        await db.UserDrafts
            .Where(d => d.UserId == userId.Value && d.FormKey == formKey)
            .ExecuteDeleteAsync();

        // Идемпотентно: удалять нечего — тоже успех, фронтенд шлёт это после каждого сохранения формы.
        return Ok(new { message = "Черновик удалён" });
    }
}

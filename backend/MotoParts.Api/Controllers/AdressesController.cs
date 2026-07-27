using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/adresses")]
[Authorize]
public class AdressesController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<AdressDto>>> List()
    {
        var userId = CurrentUserId;
        return Ok(await db.DeliveryAdresses
            .Where(a => a.UserId == userId)
            .Select(a => new AdressDto(a.Id, a.Adress, a.PostCode))
            .ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<AdressDto>> Create(CreateAdressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Adress))
            return BadRequest(new { message = "Адрес обязателен" });

        var address = new DeliveryAdress
        {
            Adress = request.Adress.Trim(),
            PostCode = request.PostCode,
            UserId = CurrentUserId,
        };
        db.DeliveryAdresses.Add(address);
        await db.SaveChangesAsync();

        return Ok(new AdressDto(address.Id, address.Adress, address.PostCode));
    }
}

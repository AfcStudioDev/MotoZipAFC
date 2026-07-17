using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressesController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<AddressDto>>> List()
    {
        var userId = CurrentUserId;
        return Ok(await db.DeliveryAddresses
            .Where(a => a.UserId == userId)
            .Select(a => new AddressDto(a.Id, a.Address, a.PostCode))
            .ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create(CreateAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { message = "Адрес обязателен" });

        var address = new DeliveryAddress
        {
            Address = request.Address.Trim(),
            PostCode = request.PostCode,
            UserId = CurrentUserId,
        };
        db.DeliveryAddresses.Add(address);
        await db.SaveChangesAsync();

        return Ok(new AddressDto(address.Id, address.Address, address.PostCode));
    }
}

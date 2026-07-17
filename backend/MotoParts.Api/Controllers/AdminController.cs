using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Api.Data;
using MotoParts.Api.DTOs;
using MotoParts.Api.Models;
using MotoParts.Api.Services;

namespace MotoParts.Api.Controllers;

/// <summary>Админ-панель: ручное добавление записей в каждую таблицу и просмотр содержимого.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    // ---------- MotoMarks ----------
    [HttpGet("marks")]
    public async Task<IActionResult> Marks() =>
        Ok(await db.MotoMarks.OrderBy(m => m.Id).Select(m => new { m.Id, m.Mark }).ToListAsync());

    [HttpPost("marks")]
    public async Task<IActionResult> AddMark(AdminMarkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mark))
            return BadRequest(new { message = "Название марки обязательно" });
        if (await db.MotoMarks.AnyAsync(m => m.Mark == request.Mark.Trim()))
            return Conflict(new { message = "Такая марка уже существует" });

        var mark = new MotoMark { Mark = request.Mark.Trim() };
        db.MotoMarks.Add(mark);
        await db.SaveChangesAsync();
        return Ok(new { mark.Id, mark.Mark });
    }

    // ---------- MotoModels ----------
    [HttpGet("models")]
    public async Task<IActionResult> Models() =>
        Ok(await db.MotoModels.OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.MarkId, m.Model, Mark = m.Mark != null ? m.Mark.Mark : null })
            .ToListAsync());

    [HttpPost("models")]
    public async Task<IActionResult> AddModel(AdminModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { message = "Название модели обязательно" });
        if (!await db.MotoMarks.AnyAsync(m => m.Id == request.MarkId))
            return BadRequest(new { message = "Марка не найдена" });

        var model = new MotoModel { MarkId = request.MarkId, Model = request.Model.Trim() };
        db.MotoModels.Add(model);
        await db.SaveChangesAsync();
        return Ok(new { model.Id, model.MarkId, model.Model });
    }

    // ---------- ZipGroups ----------
    [HttpGet("groups")]
    public async Task<IActionResult> Groups() =>
        Ok(await db.ZipGroups.OrderBy(g => g.Id).Select(g => new { g.Id, g.GroupName }).ToListAsync());

    [HttpPost("groups")]
    public async Task<IActionResult> AddGroup(AdminGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest(new { message = "Название группы обязательно" });

        var group = new ZipGroup { GroupName = request.GroupName.Trim() };
        db.ZipGroups.Add(group);
        await db.SaveChangesAsync();
        return Ok(new { group.Id, group.GroupName });
    }

    // ---------- PartNumbers ----------
    [HttpGet("partnumbers")]
    public async Task<IActionResult> PartNumbers() =>
        Ok(await db.PartNumbers.OrderBy(p => p.Id).Select(p => new { p.Id, PartNumber = p.Number }).ToListAsync());

    [HttpPost("partnumbers")]
    public async Task<IActionResult> AddPartNumber(AdminPartNumberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PartNumber))
            return BadRequest(new { message = "Парт-номер обязателен" });
        if (await db.PartNumbers.AnyAsync(p => p.Number == request.PartNumber.Trim()))
            return Conflict(new { message = "Такой парт-номер уже существует" });

        var partNumber = new PartNumber { Number = request.PartNumber.Trim() };
        db.PartNumbers.Add(partNumber);
        await db.SaveChangesAsync();
        return Ok(new { partNumber.Id, PartNumber = partNumber.Number });
    }

    // ---------- Zip ----------
    [HttpGet("zip")]
    public async Task<IActionResult> ZipList() =>
        Ok(await db.Zip.OrderBy(z => z.Name)
            .Select(z => new
            {
                z.Id, z.Name, z.Cost, z.CountStored,
                z.PartNumberId, z.MarkId, z.ModelId, z.GroupId,
                Year = z.Year != null ? z.Year.Value.Year : (int?)null,
            })
            .ToListAsync());

    [HttpPost("zip")]
    public async Task<IActionResult> AddZip(AdminZipRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Название запчасти обязательно" });
        if (request.Cost < 0 || request.CountStored < 0)
            return BadRequest(new { message = "Стоимость и количество не могут быть отрицательными" });

        var zip = new Zip
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Cost = request.Cost,
            PartNumberId = request.PartNumberId,
            MarkId = request.MarkId,
            ModelId = request.ModelId,
            GroupId = request.GroupId,
            CountStored = request.CountStored,
            Year = request.Year.HasValue ? new DateOnly(request.Year.Value, 1, 1) : null,
        };
        db.Zip.Add(zip);
        await db.SaveChangesAsync();
        return Ok(new { zip.Id, zip.Name });
    }

    // ---------- Users ----------
    [HttpGet("users")]
    public async Task<IActionResult> Users() =>
        Ok(await db.Users.OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Email, u.FIO, u.PhoneNumber, u.IsAdmin })
            .ToListAsync());

    [HttpPost("users")]
    public async Task<IActionResult> AddUser(AdminUserRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.FIO))
            return BadRequest(new { message = "Email и ФИО обязательны" });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        var user = new User
        {
            Email = email,
            FIO = request.FIO.Trim(),
            PhoneNumber = request.PhoneNumber,
            IsAdmin = request.IsAdmin,
            PasswordHash = string.IsNullOrEmpty(request.Password) ? null : PasswordHasher.Hash(request.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.FIO, user.IsAdmin });
    }

    // ---------- DeliveryAdressess ----------
    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses() =>
        Ok(await db.DeliveryAddresses.OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Address, a.PostCode, a.UserId })
            .ToListAsync());

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AdminAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { message = "Адрес обязателен" });
        if (request.UserId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.UserId))
            return BadRequest(new { message = "Пользователь не найден" });

        var address = new DeliveryAddress
        {
            Address = request.Address.Trim(),
            PostCode = request.PostCode,
            UserId = request.UserId,
        };
        db.DeliveryAddresses.Add(address);
        await db.SaveChangesAsync();
        return Ok(new { address.Id, address.Address });
    }

    // ---------- Orders ----------
    [HttpGet("orders")]
    public async Task<IActionResult> Orders() =>
        Ok(await db.Orders.OrderByDescending(o => o.OrderDateTime)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CountOrdered, o.NomenclatureId, o.AddressId, o.OrderDateTime,
                Zip = o.Nomenclature != null ? o.Nomenclature.Name : null,
            })
            .ToListAsync());

    [HttpPost("orders")]
    public async Task<IActionResult> AddOrder(AdminOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber))
            return BadRequest(new { message = "Номер заказа обязателен" });
        if (!await db.DeliveryAddresses.AnyAsync(a => a.Id == request.AddressId))
            return BadRequest(new { message = "Адрес доставки не найден" });
        if (request.NomenclatureId.HasValue && !await db.Zip.AnyAsync(z => z.Id == request.NomenclatureId))
            return BadRequest(new { message = "Запчасть не найдена" });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = request.OrderNumber.Trim(),
            CountOrdered = request.CountOrdered,
            NomenclatureId = request.NomenclatureId,
            AddressId = request.AddressId,
            OrderDateTime = request.OrderDateTime ?? DateTimeOffset.UtcNow,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.OrderNumber });
    }
}

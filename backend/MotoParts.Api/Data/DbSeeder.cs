using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Extensions;
using MotoParts.Api.Models;
using MotoParts.Api.Services;

namespace MotoParts.Api.Data;

/// <summary>
/// Начальные данные: справочники и служебные учётные записи.
/// Демонстрационный каталог намеренно не заводится — база наполняется
/// через админ-панель реальными позициями.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        await SeedDictionariesAsync(db);
        await SeedUsersAsync(db, config);
    }

    /// <summary>
    /// Справочники операций обязаны существовать до первой записи в журнал:
    /// Log.OperationId объявлен NOT NULL с внешним ключом на Operations.
    /// </summary>
    private static async Task SeedDictionariesAsync(AppDbContext db)
    {
        if (!await db.DeliveryStatuses.AnyAsync())
        {
            await db.DeliveryStatuses.AddRangeAsync(
                new DeliveryStatus { Id = 1, Description = "created" },
                new DeliveryStatus { Id = 2, Description = "sent" },
                new DeliveryStatus { Id = 3, Description = "completed" },
                new DeliveryStatus { Id = 4, Description = "canceled" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.OperationTypes.AnyAsync())
        {
            await db.OperationTypes.AddRangeAsync(
                // GetDescription(this Enum) — второй перегрузки принимает int и падает
                // на перечислениях с базовым типом short.
                Enum.GetValues<OperationTypeEnum>().Select(t => new OperationType
                {
                    Id = (short)t,
                    Description = t.GetDescription()
                })
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Operations.AnyAsync())
        {
            await db.Operations.AddRangeAsync(
                new Operation { Id = (short)OperationEnum.Income, TypeId = (short)OperationTypeEnum.StockMovement, Description = "Приход" },
                new Operation { Id = (short)OperationEnum.Sale, TypeId = (short)OperationTypeEnum.StockMovement, Description = "Продажа" },
                new Operation { Id = (short)OperationEnum.Refund, TypeId = (short)OperationTypeEnum.StockMovement, Description = "Возврат" },
                new Operation { Id = (short)OperationEnum.WriteOff, TypeId = (short)OperationTypeEnum.StockMovement, Description = "Списание" },
                new Operation { Id = (short)OperationEnum.Correction, TypeId = (short)OperationTypeEnum.StockMovement, Description = "Коррекция остатка" },
                new Operation { Id = (short)OperationEnum.Markup, TypeId = (short)OperationTypeEnum.Repricing, Description = "Наценка" },
                new Operation { Id = (short)OperationEnum.Markdown, TypeId = (short)OperationTypeEnum.Repricing, Description = "Уценка" },
                new Operation { Id = (short)OperationEnum.Other, TypeId = (short)OperationTypeEnum.Audit, Description = "Прочее" }
            );
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Служебные учётные записи. Каждая заводится только при отсутствии —
    /// пароль уже заведённого пользователя сидер не трогает.
    /// </summary>
    private static async Task SeedUsersAsync(AppDbContext db, IConfiguration config)
    {
        var adminEmail = (config["Seed:AdminEmail"] ?? "Admin").ToLowerInvariant();
        var senderEmail = (config["Seed:SenderEmail"] ?? "Sender").ToLowerInvariant();
        var registrarEmail = (config["Seed:RegistrarEmail"] ?? "Registrar").ToLowerInvariant();

        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            await db.Users.AddAsync(new User
            {
                Email = adminEmail,
                FIO = "Администратор",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:AdminPassword"] ?? "Admin123!"),
            });
        }
        if (!await db.Users.AnyAsync(u => u.Email == registrarEmail))
        {
            await db.Users.AddAsync(new User
            {
                Email = registrarEmail,
                FIO = "Регистратор",
                IsRegistrar = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:RegistrarPassword"] ?? "Registrar123!"),
            });
        }
        if (!await db.Users.AnyAsync(u => u.Email == senderEmail))
        {
            await db.Users.AddAsync(new User
            {
                Email = senderEmail,
                FIO = "Отправщик",
                IsSender = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:SenderPassword"] ?? "Sender123!"),
            });
        }
        await db.SaveChangesAsync();
    }
}

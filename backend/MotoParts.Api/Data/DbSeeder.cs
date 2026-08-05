using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Extensions;
using MotoParts.Api.Models;
using MotoParts.Api.Services;

namespace MotoParts.Api.Data;

/// <summary>Начальные данные: справочники, администратор и демо-каталог.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        await SeedDictionariesAsync(db);
        var users = await SeedUsersAsync(db, config);
        await SeedCatalogAsync(db, users);
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

    private static async Task<User> SeedUsersAsync(AppDbContext db, IConfiguration config)
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

        const string clientEmail = "client@example.com";
        var clientUser = await db.Users.FirstOrDefaultAsync(u => u.Email == clientEmail);
        if (clientUser == null)
        {
            clientUser = new User
            {
                Email = clientEmail,
                FIO = "Петров Петр Петрович",
                PhoneNumber = "+79997654321",
                PasswordHash = "100000.Jbu/lFzjTuCTS/Kral3AEg==.nr4Ap2lMMY4HifQ9+FZRpec3jQSXDbh3GrZsrll1Sm4=",
                IsAdmin = false
            };
            await db.Users.AddAsync(clientUser);
            await db.SaveChangesAsync();
        }

        if (!await db.DeliveryAddressess.AnyAsync(a => a.UserId == clientUser.Id))
        {
            await db.DeliveryAddressess.AddAsync(new DeliveryAddress
            {
                Address = "г. Москва, ул. Мотоциклетная, д. 42, кв. 10",
                PostCode = "101000",
                UserId = clientUser.Id
            });
            await db.SaveChangesAsync();
        }

        return clientUser;
    }

    private static async Task SeedCatalogAsync(AppDbContext db, User clientUser)
    {
        if (await db.Zips.AnyAsync()) return;

        // --- Справочники классификации ---
        var honda = new MotoMark { Mark = "Honda" };
        var yamaha = new MotoMark { Mark = "Yamaha" };
        var kawasaki = new MotoMark { Mark = "Kawasaki" };
        var suzuki = new MotoMark { Mark = "Suzuki" };

        var cbr = new MotoModel { Mark = honda, Model = "CBR600RR" };
        var africa = new MotoModel { Mark = honda, Model = "Africa Twin" };
        var r1 = new MotoModel { Mark = yamaha, Model = "YZF-R1" };
        var mt07 = new MotoModel { Mark = yamaha, Model = "MT-07" };
        var ninja = new MotoModel { Mark = kawasaki, Model = "Ninja ZX-10R" };
        var gsxr = new MotoModel { Mark = suzuki, Model = "GSX-R750" };

        var engine = new ZipGroup { GroupName = "Двигатель" };
        var brakes = new ZipGroup { GroupName = "Тормозная система" };
        var suspension = new ZipGroup { GroupName = "Подвеска" };
        var electrics = new ZipGroup { GroupName = "Электрика" };
        var body = new ZipGroup { GroupName = "Пластик и кузов" };

        // --- Каталожные позиции: наименование теперь живёт здесь ---
        var pn1 = new PartNumber { PartNum = "15410-MFJ-D01", Name = "Масляный фильтр", Group = engine };
        var pn2 = new PartNumber { PartNum = "5VY-13440-30", Name = "Масляный фильтр", Group = engine };
        var pn3 = new PartNumber { PartNum = "43082-0155", Name = "Тормозные колодки", Group = brakes };
        var pn4 = new PartNumber { PartNum = "59100-29G00", Name = "Амортизатор задний", Group = suspension };
        var pn5 = new PartNumber { PartNum = "38770-MKR-D12", Name = "Блок управления (ECU)", Group = electrics };

        // --- Применимость: одна позиция может подходить к нескольким моделям ---
        await db.PartNumberApplicabilities.AddRangeAsync(
            new PartNumberApplicability { PartNumber = pn1, Model = cbr },
            new PartNumberApplicability { PartNumber = pn1, Model = africa },
            new PartNumberApplicability { PartNumber = pn2, Model = r1 },
            new PartNumberApplicability { PartNumber = pn2, Model = mt07 },
            new PartNumberApplicability { PartNumber = pn3, Model = ninja },
            new PartNumberApplicability { PartNumber = pn3, Model = cbr },
            new PartNumberApplicability { PartNumber = pn4, Model = gsxr },
            new PartNumberApplicability { PartNumber = pn5, Model = africa }
        );

        // --- Доноры ---
        var incomeHonda = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda 2024" };
        var incomeYamaha = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Yamaha 2024" };
        var incomeKawasaki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Kawasaki 2024" };
        var incomeSuzuki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Suzuki 2024" };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var zips = new[]
        {
            new Zip { Id = Guid.NewGuid(), PartNumber = pn1, IncomeCost = 1250m,  SellCost = 1990m,  IncomeMoto = incomeHonda,    Year = 2020, IncomeDate = today },
            new Zip { Id = Guid.NewGuid(), PartNumber = pn3, IncomeCost = 4200m,  SellCost = 6500m,  IncomeMoto = incomeHonda,    Year = 2019, IncomeDate = today },
            new Zip { Id = Guid.NewGuid(), PartNumber = pn2, IncomeCost = 1390m,  SellCost = 2190m,  IncomeMoto = incomeYamaha,   Year = 2021, IncomeDate = today },
            new Zip { Id = Guid.NewGuid(), PartNumber = pn3, IncomeCost = 4200m,  SellCost = 6300m,  IncomeMoto = incomeKawasaki, Year = 2019, IncomeDate = today },
            new Zip { Id = Guid.NewGuid(), PartNumber = pn4, IncomeCost = 28500m, SellCost = 39900m, IncomeMoto = incomeSuzuki,   Year = 2018, IncomeDate = today },
            new Zip { Id = Guid.NewGuid(), PartNumber = pn5, IncomeCost = 54100m, SellCost = 74900m, IncomeMoto = incomeHonda,    Year = 2022, IncomeDate = today },
        };
        await db.Zips.AddRangeAsync(zips);

        // --- Остатки и приходные движения в журнале ---
        var counts = new[] { 5, 2, 3, 1, 1, 1 };
        for (int i = 0; i < zips.Length; i++)
        {
            await db.Stored.AddAsync(new Stored { Zip = zips[i], Count = counts[i] });
            await db.Logs.AddAsync(new Log
            {
                CreatedAt = DateTimeOffset.UtcNow,
                OperationId = (short)OperationEnum.Income,
                Zip = zips[i],
                Qty = counts[i],
                UnitCost = zips[i].IncomeCost,
                Description = $"Первичное оприходование: {zips[i].PartNumber.Name}"
            });
        }
        await db.SaveChangesAsync();

        // --- Демо-заказ ---
        if (await db.Orders.AnyAsync()) return;

        var clientAddress = await db.DeliveryAddressess.FirstOrDefaultAsync(a => a.UserId == clientUser.Id);
        if (clientAddress == null) return;

        var soldZip = zips[0];
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-00001",
            CountOrdered = 1,
            ZipId = soldZip.Id,
            AddressId = clientAddress.Id,
            UserId = clientUser.Id,
            OrderDateTime = DateTimeOffset.UtcNow,
            SellCost = soldZip.SellCost ?? 0m,
            Discount = 0m,
            OperationId = (short)OperationEnum.Sale,
            DeliveryStatusId = 1 // created
        };
        await db.Orders.AddAsync(order);

        var stored = await db.Stored.FirstAsync(s => s.ZipId == soldZip.Id);
        stored.Count -= order.CountOrdered;

        await db.Logs.AddAsync(new Log
        {
            CreatedAt = DateTimeOffset.UtcNow,
            OperationId = (short)OperationEnum.Sale,
            OrderId = order.Id,
            ZipId = soldZip.Id,
            UserId = clientUser.Id,
            Qty = -order.CountOrdered,
            UnitCost = soldZip.IncomeCost,
            SellCost = order.SellCost,
            Description = $"Заказ {order.OrderNumber} создан"
        });

        await db.SaveChangesAsync();
    }
}

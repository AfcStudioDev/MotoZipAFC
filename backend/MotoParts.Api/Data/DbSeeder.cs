using Microsoft.EntityFrameworkCore;

using MotoParts.Api.Models;
using MotoParts.Api.Services;

namespace MotoParts.Api.Data;

/// <summary>Начальные данные: администратор и демо-каталог.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        // Администратор по умолчанию (email/пароль настраиваются в appsettings)
        var adminEmail = (config["Seed:AdminEmail"] ?? "Admin").ToLowerInvariant();
        var senderEmail = (config["Seed:SenderEmail"] ?? "Sender").ToLowerInvariant();
        var registrarEmail = (config["Seed:RegistrarEmail"] ?? "Registrar").ToLowerInvariant();

        // СИДИРОВАНИЕ СПРАВОЧНИКОВ (Статусы и Операции)
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

        if (!await db.Operations.AnyAsync())
        {
            await db.Operations.AddRangeAsync(
                new Operation { Id = 1, Type = 1, Description = "Продажа" },
                new Operation { Id = 2, Type = 2, Description = "Возврат" },
                new Operation { Id = 3, Type = 3, Description = "Приход на склад" }
            );
            await db.SaveChangesAsync();
        }

        // ID запчастей в переменные, чтобы могли на них сослаться в заказах
        var zip1Id = Guid.NewGuid();
        var zip2Id = Guid.NewGuid();

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

        var clientEmail = "client@example.com";
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
            await db.SaveChangesAsync(); // Сохраняем, чтобы сгенерировался числовой Id клиента
        }

        // Добавляем адрес для клиента
        if (clientUser != null && !await db.DeliveryAdressess.AnyAsync(a => a.UserId == clientUser.Id))
        {
            await db.DeliveryAdressess.AddAsync(new DeliveryAdress
            {
                Adress = "г. Москва, ул. Мотоциклетная, д. 42, кв. 10",
                PostCode = "101000",
                UserId = clientUser.Id
            });
            await db.SaveChangesAsync(); // Сохраняем адрес, чтобы получить его ID
        }

        if (!await db.MotoMarks.AnyAsync())
        {
            var honda = new MotoMark { Mark = "Honda" };
            var yamaha = new MotoMark { Mark = "Yamaha" };
            var kawasaki = new MotoMark { Mark = "Kawasaki" };
            var suzuki = new MotoMark { Mark = "Suzuki" };
            var bmw = new MotoMark { Mark = "BMW" };
            await db.MotoMarks.AddRangeAsync(honda, yamaha, kawasaki, suzuki, bmw);

            var cbr = new MotoModel { Mark = honda, Model = "CBR600RR" };
            var africa = new MotoModel { Mark = honda, Model = "Africa Twin" };
            var r1 = new MotoModel { Mark = yamaha, Model = "YZF-R1" };
            var mt07 = new MotoModel { Mark = yamaha, Model = "MT-07" };
            var ninja = new MotoModel { Mark = kawasaki, Model = "Ninja ZX-10R" };
            var gsxr = new MotoModel { Mark = suzuki, Model = "GSX-R750" };
            await db.MotoModels.AddRangeAsync(cbr, africa, r1, mt07, ninja, gsxr);

            var engine = new ZipGroup { GroupName = "Двигатель" };
            var brakes = new ZipGroup { GroupName = "Тормозная система" };
            var suspension = new ZipGroup { GroupName = "Подвеска" };
            var electrics = new ZipGroup { GroupName = "Электрика" };
            var body = new ZipGroup { GroupName = "Пластик и кузов" };
            await db.ZipGroups.AddRangeAsync(engine, brakes, suspension, electrics, body);

            var pn1 = new PartNumber { PartNum = "15410-MFJ-D01" };
            var pn2 = new PartNumber { PartNum = "5VY-13440-30" };
            var pn3 = new PartNumber { PartNum = "43082-0155" };
            var pn4 = new PartNumber { PartNum = "59100-29G00" };
            var pn5 = new PartNumber { PartNum = "38770-MKR-D12" };
            await db.PartNumbers.AddRangeAsync(pn1, pn2, pn3, pn4, pn5);

            var incomeHonda = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda 2024" };
            var incomeYamaha = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Yamaha 2024" };
            var incomeKawasaki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Kawasaki 2024" };
            var incomeSuzuki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Suzuki 2024" };
            await db.Zips.AddRangeAsync(
                new Zip
                {
                    Id = zip1Id,
                    Name = "Масляный фильтр Honda CBR600RR",
                    IncomeCost = 1250,
                    PartNumber = pn1,
                    Mark = honda,
                    Model = cbr,
                    Group = engine,
                    IncomeMoto = incomeHonda,
                    Year = new DateOnly(2020, 1, 1),
                },
                new Zip
                {
                    Id = zip2Id,
                    Name = "Тормозные колодки Honda CBR600RR",
                    IncomeCost = 4200,
                    PartNumber = pn3,
                    Mark = kawasaki,
                    Model = ninja,
                    Group = brakes,
                    IncomeMoto = incomeHonda,
                    Year = new DateOnly(2019, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(),
                    Name = "Масляный фильтр Yamaha YZF-R1",
                    IncomeCost = 1390,
                    PartNumber = pn2,
                    Mark = yamaha,
                    Model = r1,
                    Group = engine,
                    IncomeMoto = incomeYamaha,
                    Year = new DateOnly(2021, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(),
                    Name = "Тормозные колодки Kawasaki Ninja ZX-10R",
                    IncomeCost = 4200,
                    PartNumber = pn3,
                    Mark = kawasaki,
                    Model = ninja,
                    Group = brakes,
                    IncomeMoto = incomeKawasaki,
                    Year = new DateOnly(2019, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(),
                    Name = "Амортизатор задний Suzuki GSX-R750",
                    IncomeCost = 28500,
                    PartNumber = pn4,
                    Mark = suzuki,
                    Model = gsxr,
                    Group = suspension,
                    IncomeMoto = incomeSuzuki,
                    Year = new DateOnly(2018, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(),
                    Name = "Блок управления (ECU) Honda Africa Twin",
                    IncomeCost = 54100,
                    PartNumber = pn5,
                    Mark = honda,
                    Model = africa,
                    Group = electrics,
                    IncomeMoto = incomeHonda,
                    Year = new DateOnly(2022, 1, 1),
                });
        }

        var zips = await db.Zips.Take(2).ToListAsync();
        if (zips.Count < 1) return;

        zip1Id = zips[0].Id;
        zip2Id = zips.Count > 1 ? zips[1].Id : zips[0].Id;
        
        // 4. СИДИРОВАНИЕ ОСТАТКОВ НА СКЛАДЕ (Stored)
        if (!await db.Stored.AnyAsync())
        {
            var storedItem1 = new Stored { ZipId = zip1Id, Count = 5 };
            var storedItem2 = new Stored { ZipId = zip2Id, Count = 2 };
            await db.Stored.AddRangeAsync(storedItem1, storedItem2);
            await db.SaveChangesAsync();
        }

        if (!await db.Orders.AnyAsync())
        {
            var clientAddress = await db.DeliveryAdressess.FirstOrDefaultAsync(a => a.UserId == clientUser!.Id);

            if (clientAddress != null && clientUser != null)
            {
                var orderId = Guid.NewGuid();

                // Создаем заказ (теперь включает в себя поля из старого Movement)
                var order = new Order
                {
                    Id = orderId,
                    OrderNumber = "ORD-00001",
                    CountOrdered = 1,
                    NomenclatureId = zip1Id,
                    AdressId = clientAddress.Id,
                    UserId = clientUser.Id,
                    OrderDateTime = DateTimeOffset.UtcNow,
                    SellCost = 1250m,
                    Discount = 0,
                    OperationTypeId = 1, // 1 - Продажа
                    DeliveryStatusId = 1 // 1 - created (Создан)
                };
                await db.Orders.AddAsync(order);

                // Оплата
                //var payment = new Payment
                //{
                //    Id = Guid.NewGuid(),
                //    OrderId = orderId,
                //    YooKassaPaymentId = "2412312-321321-41241-231321",
                //    Status = "succeeded",
                //    Amount = 1250m,
                //    CreatedAt = DateTimeOffset.UtcNow
                //};
                //await db.Payments.AddAsync(payment);

                // Лог заказа (новая таблица Log)
                var log = new Log
                {
                    OrderId = orderId,
                    Description = "Заказ успешно создан и оплачен клиентом."
                };
                await db.Logs.AddAsync(log);

                await db.SaveChangesAsync();
            }
        }

        //if (!await db.Operations.AnyAsync())
        //{
        //    var opSale = new Operation { Id = 1, Type = 1, Description = "Продажа" };   // 1 - Продажа
        //    var opRefund = new Operation { Id = 2, Type = 2, Description = "Возврат" }; // 2 - Возврат
        //    var opSupply = new Operation { Id = 3, Type = 3, Description = "Приход на склад" }; // 3 - Приход на склад
        //    await db.Operations.AddRangeAsync(opSale, opRefund, opSupply);
        //    await db.SaveChangesAsync(); // Сразу сохраняем справочник
        //}



        // Финальное сохранение всего, что могло остаться в памяти
        await db.SaveChangesAsync();
    }
}

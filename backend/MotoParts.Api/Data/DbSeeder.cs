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
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            db.Users.Add(new User
            {
                Email = adminEmail,
                FIO = "Администратор",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:AdminPassword"] ?? "Admin123!"),
            });
        }
        if (!await db.Users.AnyAsync(u => u.Email == registrarEmail))
        {
            db.Users.Add(new User
            {
                Email = registrarEmail,
                FIO = "Регистратор",
                IsRegistrar = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:RegistrarPassword"] ?? "Registrar123!"),
            });
        }
        if (!await db.Users.AnyAsync(u => u.Email == senderEmail))
        {
            db.Users.Add(new User
            {
                Email = senderEmail,
                FIO = "Отправщик",
                IsSender = true,
                PasswordHash = PasswordHasher.Hash(config["Seed:SenderPassword"] ?? "Sender123!"),
            });
        }


        if (!await db.MotoMarks.AnyAsync())
        {
            var honda = new MotoMark { Mark = "Honda" };
            var yamaha = new MotoMark { Mark = "Yamaha" };
            var kawasaki = new MotoMark { Mark = "Kawasaki" };
            var suzuki = new MotoMark { Mark = "Suzuki" };
            var bmw = new MotoMark { Mark = "BMW" };
            db.MotoMarks.AddRange(honda, yamaha, kawasaki, suzuki, bmw);

            var cbr = new MotoModel { Mark = honda, Model = "CBR600RR" };
            var africa = new MotoModel { Mark = honda, Model = "Africa Twin" };
            var r1 = new MotoModel { Mark = yamaha, Model = "YZF-R1" };
            var mt07 = new MotoModel { Mark = yamaha, Model = "MT-07" };
            var ninja = new MotoModel { Mark = kawasaki, Model = "Ninja ZX-10R" };
            var gsxr = new MotoModel { Mark = suzuki, Model = "GSX-R750" };
            db.MotoModels.AddRange(cbr, africa, r1, mt07, ninja, gsxr);

            var engine = new ZipGroup { GroupName = "Двигатель" };
            var brakes = new ZipGroup { GroupName = "Тормозная система" };
            var suspension = new ZipGroup { GroupName = "Подвеска" };
            var electrics = new ZipGroup { GroupName = "Электрика" };
            var body = new ZipGroup { GroupName = "Пластик и кузов" };
            db.ZipGroups.AddRange(engine, brakes, suspension, electrics, body);

            var pn1 = new PartNumber { PartNum = "15410-MFJ-D01" };
            var pn2 = new PartNumber { PartNum = "5VY-13440-30" };
            var pn3 = new PartNumber { PartNum = "43082-0155" };
            var pn4 = new PartNumber { PartNum = "59100-29G00" };
            var pn5 = new PartNumber { PartNum = "38770-MKR-D12" };
            db.PartNumbers.AddRange(pn1, pn2, pn3, pn4, pn5);

            var incomeHonda = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Honda 2024" };
            var incomeYamaha = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Yamaha 2024" };
            var incomeKawasaki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Kawasaki 2024" };
            var incomeSuzuki = new IncomeMoto { Id = Guid.NewGuid(), Description = "Поступление Suzuki 2024" };
            db.Zips.AddRange(
                new Zip
                {
                    Id = Guid.NewGuid(),
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
                    Id = Guid.NewGuid(),
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

        await db.SaveChangesAsync();
    }
}

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
        var adminEmail = (config["Seed:AdminEmail"] ?? "admin@motoparts.local").ToLowerInvariant();
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

        if (!await db.MotoMarks.AnyAsync())
        {
            var honda = new MotoMark { Mark = "Honda" };
            var yamaha = new MotoMark { Mark = "Yamaha" };
            var kawasaki = new MotoMark { Mark = "Kawasaki" };
            var suzuki = new MotoMark { Mark = "Suzuki" };
            db.MotoMarks.AddRange(honda, yamaha, kawasaki, suzuki);

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

            var pn1 = new PartNumber { Number = "15410-MFJ-D01" };
            var pn2 = new PartNumber { Number = "5VY-13440-30" };
            var pn3 = new PartNumber { Number = "43082-0155" };
            var pn4 = new PartNumber { Number = "59100-29G00" };
            var pn5 = new PartNumber { Number = "38770-MKR-D12" };
            db.PartNumbers.AddRange(pn1, pn2, pn3, pn4, pn5);

            db.Zip.AddRange(
                new Zip
                {
                    Id = Guid.NewGuid(), Name = "Масляный фильтр Honda CBR600RR", Cost = 1250,
                    PartNumber = pn1, Mark = honda, Model = cbr, Group = engine,
                    CountStored = 40, Year = new DateOnly(2020, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(), Name = "Масляный фильтр Yamaha YZF-R1", Cost = 1390,
                    PartNumber = pn2, Mark = yamaha, Model = r1, Group = engine,
                    CountStored = 25, Year = new DateOnly(2021, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(), Name = "Тормозные колодки Kawasaki Ninja ZX-10R", Cost = 4200,
                    PartNumber = pn3, Mark = kawasaki, Model = ninja, Group = brakes,
                    CountStored = 12, Year = new DateOnly(2019, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(), Name = "Амортизатор задний Suzuki GSX-R750", Cost = 28500,
                    PartNumber = pn4, Mark = suzuki, Model = gsxr, Group = suspension,
                    CountStored = 3, Year = new DateOnly(2018, 1, 1),
                },
                new Zip
                {
                    Id = Guid.NewGuid(), Name = "Блок управления (ECU) Honda Africa Twin", Cost = 54100,
                    PartNumber = pn5, Mark = honda, Model = africa, Group = electrics,
                    CountStored = 2, Year = new DateOnly(2022, 1, 1),
                });
        }

        await db.SaveChangesAsync();
    }
}

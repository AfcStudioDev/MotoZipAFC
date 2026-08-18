using Microsoft.EntityFrameworkCore;

using MotoParts.Domain.Models;

namespace MotoParts.Application.Abstractions;

/// <summary>
/// То, что слою сценариев нужно от хранилища. Отдельного репозитория на каждую таблицу нет
/// намеренно: DbContext сам по себе — Unit of Work, а DbSet — репозиторий, и оборачивать их
/// ещё раз значит писать код без выгоды. Интерфейс нужен ровно для того, чтобы Application
/// не ссылался на Infrastructure — направление зависимостей теперь стережёт компилятор.
/// </summary>
public interface IAppDbContext
{
    DbSet<MotoMark> MotoMarks { get; }
    DbSet<PartNumber> PartNumbers { get; }
    DbSet<PartNumberApplicability> PartNumberApplicabilities { get; }
    DbSet<MotoSeries> MotoSeries { get; }
    DbSet<PartNumberSeriesApplicability> PartNumberSeriesApplicabilities { get; }
    DbSet<ZipGroup> ZipGroups { get; }
    DbSet<MotoModel> MotoModels { get; }
    DbSet<Zip> Zips { get; }
    DbSet<User> Users { get; }
    DbSet<DeliveryAddress> DeliveryAddressess { get; }
    DbSet<OperationType> OperationTypes { get; }
    DbSet<Operation> Operations { get; }
    DbSet<Stored> Stored { get; }
    DbSet<IncomeMoto> IncomeMotos { get; }
    DbSet<Order> Orders { get; }
    DbSet<Log> Logs { get; }
    DbSet<PriceHistory> PriceHistories { get; }
    DbSet<DeliveryStatus> DeliveryStatuses { get; }
    DbSet<ZipPhoto> ZipPhotos { get; }
    DbSet<UserDraft> UserDrafts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

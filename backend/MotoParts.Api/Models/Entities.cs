using MotoParts.Api.Extensions;

using System.ComponentModel.DataAnnotations;

namespace MotoParts.Api.Models;

public class Order
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string OrderNumber { get; set; } = null!;

    public int CountOrdered { get; set; } = 0;

    public Guid ZipId { get; set; }
    public int AddressId { get; set; }
    public DateTimeOffset OrderDateTime { get; set; }

    public decimal SellCost { get; set; }
    public short? OperationId { get; set; }
    public decimal? Discount { get; set; }
    public int UserId { get; set; }
    public short? DeliveryStatusId { get; set; }

    // Навигационные свойства
    public Zip Zip { get; set; } = null!;
    public DeliveryAddress Address { get; set; } = null!;
    public Operation? Operation { get; set; }
    public User User { get; set; } = null!;
    public DeliveryStatus? DeliveryStatus { get; set; }

    public ICollection<Log> Logs { get; set; } = new HashSet<Log>();
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string YooKassaPaymentId { get; set; } = null!;
    public string Status { get; set; } = 0.GetDescription<PaymentStatusEnum>(); // pending | waiting_for_capture | succeeded | canceled
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Order Order { get; set; } = null!;
}

public class MotoMark
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Mark { get; set; } = null!;

    public ICollection<MotoModel> MotoModels { get; set; } = new HashSet<MotoModel>();
}

public class MotoModel
{
    [Key]
    public int Id { get; set; }

    public int? MarkId { get; set; }
    public MotoMark? Mark { get; set; }

    public string Model { get; set; } = null!;

    public ICollection<PartNumberApplicability> Applicability { get; set; } = new HashSet<PartNumberApplicability>();
}

public class ZipGroup
{
    [Key]
    public int Id { get; set; }

    public string GroupName { get; set; } = null!;

    public ICollection<PartNumber> PartNumbers { get; set; } = new HashSet<PartNumber>();
}

/// <summary>Каталожная позиция: номер, наименование и группа. Применимость к моделям — в PartNumberApplicability.</summary>
public class PartNumber
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string PartNum { get; set; } = null!;

    [Required]
    public string Name { get; set; } = null!;

    public int? GroupId { get; set; }
    public ZipGroup? Group { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
    public ICollection<PartNumberApplicability> Applicability { get; set; } = new HashSet<PartNumberApplicability>();
}

/// <summary>Применимость каталожной позиции к моделям мотоциклов (многие-ко-многим).</summary>
public class PartNumberApplicability
{
    [Key]
    public int Id { get; set; }

    public int PartNumId { get; set; }
    public PartNumber PartNumber { get; set; } = null!;

    public int ModelId { get; set; }
    public MotoModel Model { get; set; } = null!;
}

/// <summary>Конкретная физическая деталь, снятая с донора.</summary>
public class Zip
{
    [Key]
    public Guid Id { get; set; }

    public decimal IncomeCost { get; set; }

    public decimal? SellCost { get; set; }

    public int PartNumId { get; set; }
    public PartNumber PartNumber { get; set; } = null!;

    public short? Year { get; set; }

    public Guid IncomeMotoId { get; set; }
    public IncomeMoto IncomeMoto { get; set; } = null!;

    public DateOnly? IncomeDate { get; set; }

    public string? Comment { get; set; }

    public Stored? Stored { get; set; }

    public ICollection<Order> Orders { get; set; } = new HashSet<Order>();
    public ICollection<ZipPhoto> Photos { get; set; } = new HashSet<ZipPhoto>();
    public ICollection<Log> Logs { get; set; } = new HashSet<Log>();
    public ICollection<PriceHistory> PriceHistory { get; set; } = new HashSet<PriceHistory>();
}

/// <summary>Журнал операций: движения товара и события аудита.</summary>
public class Log
{
    [Key]
    public int Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public short OperationId { get; set; }
    public Operation Operation { get; set; } = null!;

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? ZipId { get; set; }
    public Zip? Zip { get; set; }

    /// <summary>Изменение количества со знаком: +5 приход, −2 продажа. null — событие без движения товара.</summary>
    public int? Qty { get; set; }

    public decimal? UnitCost { get; set; }
    public decimal? SellCost { get; set; }

    public string? Description { get; set; }
}

/// <summary>История переоценки: наценка и уценка.</summary>
public class PriceHistory
{
    [Key]
    public long Id { get; set; }

    public Guid ZipId { get; set; }
    public Zip Zip { get; set; } = null!;

    public decimal OldCost { get; set; }
    public decimal NewCost { get; set; }

    public short OperationId { get; set; }
    public Operation Operation { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Comment { get; set; }
}

public class DeliveryStatus
{
    [Key]
    public short Id { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    public ICollection<Order> Orders { get; set; } = new HashSet<Order>();
}

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Email { get; set; } = null!;

    // Поля авторизации
    public string? PasswordHash { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }

    public bool IsAdmin { get; set; } = false;
    public bool IsSender { get; set; } = false;
    public bool IsRegistrar { get; set; } = false;

    [Required]
    public string FIO { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? OAuthProvider { get; set; }       // "google" | "vk" | null
    public string? OAuthSubject { get; set; }        // внешний id пользователя у провайдера

    public ICollection<DeliveryAddress> DeliveryAddresses { get; set; } = new HashSet<DeliveryAddress>();
    public ICollection<Order> Orders { get; set; } = new HashSet<Order>();
    public ICollection<IncomeMoto> IncomeMotos { get; set; } = new HashSet<IncomeMoto>();
}

public class DeliveryAddress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Address { get; set; } = null!;

    public string? PostCode { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<Order> Orders { get; set; } = new HashSet<Order>();
}

/// <summary>Категория операции: движение товара / переоценка / аудит.</summary>
public class OperationType
{
    [Key]
    public short Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public ICollection<Operation> Operations { get; set; } = new HashSet<Operation>();
}

/// <summary>Конкретная операция: приход, продажа, списание, коррекция, наценка, уценка.</summary>
public class Operation
{
    [Key]
    public short Id { get; set; }

    public short? TypeId { get; set; }
    public OperationType? Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public ICollection<Order> Orders { get; set; } = new HashSet<Order>();
    public ICollection<Log> Logs { get; set; } = new HashSet<Log>();
    public ICollection<PriceHistory> PriceHistory { get; set; } = new HashSet<PriceHistory>();
}

/// <summary>Текущий остаток по конкретной детали. Источник правды по истории — Log.</summary>
public class Stored
{
    [Key]
    public int Id { get; set; }

    public Guid ZipId { get; set; }
    public Zip Zip { get; set; } = null!;

    public int Count { get; set; }
}

/// <summary>Донор: мотоцикл, с которого сняты детали.</summary>
public class IncomeMoto
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}

public class ZipPhoto
{
    [Key]
    public int Id { get; set; }

    public Guid ZipId { get; set; }
    public Zip Zip { get; set; } = null!;

    public string FileName { get; set; } = null!;

    /// <summary>
    /// Флаг для главной картинки
    /// </summary>
    public bool IsMain { get; set; }
}

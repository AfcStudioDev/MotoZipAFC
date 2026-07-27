using MotoParts.Api.Extensions;

using System.ComponentModel.DataAnnotations;

namespace MotoParts.Api.Models;
public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public int CountOrdered { get; set; }
    public Guid? NomenclatureId { get; set; }
    public int AdressId { get; set; }
    public DateTimeOffset OrderDateTime { get; set; }
    public decimal? SellCost { get; set; }
    public Zip? Nomenclature { get; set; }
    public DeliveryAdress Address { get; set; } = null!;
    public Payment? Payment { get; set; }
    public string DeliveryStatus { get; set; } = DeliveryStatusEnum.created.GetDescription(); // created | sent | completed | canceled
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
    public string Mark { get; set; }

    public ICollection<MotoModel> MotoModels { get; set; } = new HashSet<MotoModel>();
    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}

public class PartNumber
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string PartNum { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}

public class ZipGroup
{
    [Key]
    public int Id { get; set; }

    public string GroupName { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}

public class MotoModel
{
    [Key]
    public int Id { get; set; }

    public int? MarkId { get; set; }
    public MotoMark Mark { get; set; }

    public string Model { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}

public class Zip
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; }

    public decimal IncomeCost { get; set; }

    public int? PartNumberId { get; set; }
    public PartNumber PartNumber { get; set; }

    public int? MarkId { get; set; }
    public MotoMark Mark { get; set; }

    public int? ModelId { get; set; }
    public MotoModel Model { get; set; }

    public int? GroupId { get; set; }
    public ZipGroup Group { get; set; }

    public DateOnly? Year { get; set; }

    public Guid IncomeMotoId { get; set; }
    public IncomeMoto IncomeMoto { get; set; }

    public ICollection<Movement> Movements { get; set; } = new HashSet<Movement>();
    public ICollection<Stored> StoredItems { get; set; } = new HashSet<Stored>();
}

public class Movement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string OrderNumber { get; set; }

    public int CountOrdered { get; set; } = 0;

    public Guid? NomenclatureId { get; set; }
    public Zip Nomenclature { get; set; }

    public int AddressId { get; set; }
    public DeliveryAdress Address { get; set; }

    public DateTime OrderDateTime { get; set; }

    public short? OperationTypeId { get; set; }
    public Operation OperationType { get; set; }

    public decimal SellCost { get; set; }
    public decimal? Discount { get; set; }
}

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Email { get; set; }

    // Восстановленные поля авторизации
    public string PasswordHash { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }

    public bool IsAdmin { get; set; } = false;
    public bool IsSender { get; set; } = false;
    public bool IsRegistrar { get; set; } = false;

    [Required]
    public string FIO { get; set; }

    public string? PhoneNumber { get; set; }

    public ICollection<DeliveryAdress> DeliveryAddresses { get; set; } = new HashSet<DeliveryAdress>();

    public string? OAuthProvider { get; set; }       // "google" | "vk" | null

    public string? OAuthSubject { get; set; }        // внешний id пользователя у провайдера
}

public class DeliveryAdress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Adress { get; set; }

    public string PostCode { get; set; }

    public int? UserId { get; set; }
    public User User { get; set; }

    public ICollection<Movement> Movements { get; set; } = new HashSet<Movement>();
}

public class Operation
{
    [Key]
    public short Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public short? Type { get; set; }

    public ICollection<Movement> Movements { get; set; } = new HashSet<Movement>();
}

public class Stored
{
    [Key]
    public int Id { get; set; }

    public Guid ZipId { get; set; }
    public Zip Zip { get; set; }

    public int Count { get; set; }
}

public class IncomeMoto
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Description { get; set; }

    public ICollection<Zip> Zips { get; set; } = new HashSet<Zip>();
}
namespace MotoParts.Api.Models;

/// <summary>Марки мотоциклов (MotoMarks).</summary>
public class MotoMark
{
    public int Id { get; set; }
    public string Mark { get; set; } = null!;

    public ICollection<MotoModel> Models { get; set; } = new List<MotoModel>();
}

/// <summary>Парт-номера запчастей (PartNumbers).</summary>
public class PartNumber
{
    public int Id { get; set; }
    public string Number { get; set; } = null!;
}

/// <summary>Группы ZIP-запчастей (ZipGroups).</summary>
public class ZipGroup
{
    public int Id { get; set; }
    public string GroupName { get; set; } = null!;
}

/// <summary>Модели мотоциклов (MotoModels).</summary>
public class MotoModel
{
    public int Id { get; set; }
    public int? MarkId { get; set; }
    public string Model { get; set; } = null!;

    public MotoMark? Mark { get; set; }
}

/// <summary>Запчасти (Zip).</summary>
public class Zip
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Cost { get; set; }
    public int? PartNumberId { get; set; }
    public int? MarkId { get; set; }
    public int? ModelId { get; set; }
    public int? GroupId { get; set; }
    public int CountStored { get; set; }
    public DateOnly? Year { get; set; }

    public PartNumber? PartNumber { get; set; }
    public MotoMark? Mark { get; set; }
    public MotoModel? Model { get; set; }
    public ZipGroup? Group { get; set; }
}

/// <summary>Заказы (Orders).</summary>
public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public int CountOrdered { get; set; }
    public Guid? NomenclatureId { get; set; }
    public int AddressId { get; set; }
    public DateTimeOffset OrderDateTime { get; set; }

    public Zip? Nomenclature { get; set; }
    public DeliveryAddress Address { get; set; } = null!;
    public Payment? Payment { get; set; }
}

/// <summary>Пользователи (Users). Поля аутентификации расширяют базовую схему.</summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public string FIO { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    // --- расширение для аутентификации ---
    public string? PasswordHash { get; set; }
    public string? OAuthProvider { get; set; }       // "google" | "vk" | null
    public string? OAuthSubject { get; set; }        // внешний id пользователя у провайдера
    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }

    public ICollection<DeliveryAddress> Addresses { get; set; } = new List<DeliveryAddress>();
}

/// <summary>Адреса доставки (DeliveryAdressess — имя таблицы сохранено как в схеме).</summary>
public class DeliveryAddress
{
    public int Id { get; set; }
    public string Address { get; set; } = null!;
    public string? PostCode { get; set; }
    public int? UserId { get; set; }

    public User? User { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>Платежи ЮKassa — отдельная таблица, не изменяющая исходную схему.</summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string YooKassaPaymentId { get; set; } = null!;
    public string Status { get; set; } = "pending"; // pending | waiting_for_capture | succeeded | canceled
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
}

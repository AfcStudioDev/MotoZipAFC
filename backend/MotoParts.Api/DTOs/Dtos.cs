namespace MotoParts.Api.DTOs;

// ---------- Auth ----------
public record RegisterRequest(string Email, string Password, string FIO, string? PhoneNumber);
public record LoginRequest(string Email, string Password);
public record GoogleLoginRequest(string IdToken);
public record VkLoginRequest(string Code, string RedirectUri);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record AuthResponse(string Token, UserDto User);

public record UserDto(int Id, string Email, string FIO, string? PhoneNumber, bool IsAdmin, bool IsRegistrar, bool IsSender);

// ---------- Catalog ----------
public record ZipDto(
    Guid Id,
    string Name,
    decimal IncomeCost,
    decimal? SellCost,
    string? PartNum,
    /// <summary>Марки, к которым применима деталь — через PartNumberApplicability.</summary>
    List<string> Marks,
    /// <summary>Модели, к которым применима деталь — через PartNumberApplicability.</summary>
    List<string> Models,
    string? Group,
    short? Year,
    Guid IncomeMotoId,
    int CountStored,
    List<string> Photos,
    /// <summary>Заметка о состоянии конкретной детали — заполняется в админке.</summary>
    string? Comment);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
}

// ---------- Orders ----------
public record CreateOrderRequest(Guid ZipId, int Count, decimal SellCost, int AddressId);
public record OrderDto(
    Guid Id,
    string OrderNumber,
    int CountOrdered,
    DateTimeOffset OrderDateTime,
    string? ZipName,
    decimal? ZipCost,
    string Address,
    //string? PaymentStatus,
    decimal? SellCost,
    decimal? Discount);

public record GuestCreateOrderRequest(
    Guid ZipId,
    int Count,
    string Fio,
    string Email,
    string Phone,
    string? Password,
    string Address,
    string? PostCode,
    decimal? Promo
    );

// ---------- Adresses ----------
public record CreateAddressRequest(string Address, string? PostCode);
public record AddressDto(int Id, string Address, string? PostCode);

// ---------- Payments ----------
public record CreatePaymentRequest(Guid OrderId, string ReturnUrl);
public record CreatePaymentResponse(string PaymentId, string ConfirmationUrl);

// ---------- Admin ----------
public record AdminMarkRequest(string Mark);
public record AdminModelRequest(int MarkId, string Model);
public record AdminGroupRequest(string GroupName);
public record AdminPartNumberRequest(string PartNum, string Name, int? GroupId);
public record AdminZipRequest(
    decimal IncomeCost,
    decimal? SellCost,
    int PartNumId,
    int CountStored,
    short? Year,
    Guid IncomeMotoId,
    DateOnly? IncomeDate,
    string? Comment);
public record AdminUserRequest(string Email, string FIO, string? PhoneNumber, bool IsAdmin, bool IsRegistrar, bool IsSender, string? Password);
public record AdminAddressRequest(string Address, string? PostCode, int? UserId);
public record AdminOrderRequest(
    string OrderNumber,
    int CountOrdered,
    Guid ZipId,
    int AddressId,
    DateTimeOffset? OrderDateTime,
    decimal SellCost,
    short? OperationId,
    decimal? Discount,
    int UserId,
    short? DeliveryStatusId
);

/// <summary>Привязка каталожной позиции к модели мотоцикла.</summary>
public record AdminApplicabilityRequest(int PartNumId, int ModelId);

public record AdminSeriesRequest(string SeriesName);

/// <summary>Привязка каталожной позиции к серии — независимо от привязки к моделям.</summary>
public record AdminSeriesApplicabilityRequest(int PartNumId, Guid SeriesId);

/// <summary>Ручная коррекция остатка: Delta со знаком, причина обязательна.</summary>
public record AdminCorrectionRequest(Guid ZipId, int Delta, string Comment);

/// <summary>Изменение цены продажи с записью в историю переоценки.</summary>
public record AdminRepriceRequest(Guid ZipId, decimal NewCost, string? Comment);

/// <summary>
/// Массовая переоценка всех запчастей одного парт-номера.
/// Используется, когда при заведении запчасти оператор выбрал вариант
/// «обновить цены для всех существующих».
/// </summary>
public record AdminRepricePartNumRequest(int PartNumId, decimal NewCost, Guid? ExceptZipId, string? Comment);

public class UpdateUserRolesRequest
{
    public bool IsSender { get; set; }
    public bool IsRegistrar { get; set; }
}

public class ZipPhotoDto
{ 
    public long Id { get; set; }
    public string Name { get; set; } = null!;
}
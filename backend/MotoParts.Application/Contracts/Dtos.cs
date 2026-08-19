namespace MotoParts.Application.Contracts;

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

// ---------- Purchases (корзина) ----------

/// <summary>Одна позиция корзины при оформлении — одна покупка может содержать несколько.</summary>
public record CartItemRequest(Guid ZipId, int Count, decimal SellCost);

/// <summary>Оформление покупки авторизованным пользователем — из одной позиции (обычная «Купить») или из нескольких (корзина).</summary>
public record CheckoutRequest(
    List<CartItemRequest> Items,
    int AddressId,
    string? DeliveryCompany = null,
    string? DeliveryComment = null);

/// <summary>То же самое для гостя — совмещает регистрацию/поиск покупателя по телефону с оформлением.</summary>
public record GuestCheckoutRequest(
    List<CartItemRequest> Items,
    string Fio,
    string Email,
    string Phone,
    string? Password,
    string Address,
    string? PostCode,
    string? DeliveryCompany = null,
    string? DeliveryComment = null);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    int CountOrdered,
    DateTimeOffset OrderDateTime,
    string? ZipName,
    decimal? ZipCost,
    string Address,
    decimal? SellCost,
    decimal? Discount,
    /// <summary>Общие на всю покупку — берутся из Purchase, одинаковы у всех её позиций.</summary>
    bool IsPaid,
    string? ReceiptFileName,
    Guid PurchaseId,
    string PurchaseNumber);

/// <summary>Покупка целиком — то, что показывает модалка оплаты сразу после оформления.</summary>
public record PurchaseDto(
    Guid Id,
    string PurchaseNumber,
    List<OrderDto> Items);

// ---------- Adresses ----------
public record CreateAddressRequest(string Address, string? PostCode);
public record AddressDto(int Id, string Address, string? PostCode);

// ---------- Payments ----------
public record CreatePaymentRequest(Guid OrderId, string ReturnUrl);
public record CreatePaymentResponse(string PaymentId, string ConfirmationUrl);

/// <summary>Номер карты для ручного перевода — показывается в модалке оплаты (см. PurchasesController.PaymentInfo).</summary>
public record PaymentInfoDto(string CardNumber);
public record UploadReceiptResponse(string ReceiptFileName);

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
    string? OrderNumber,
    int CountOrdered,
    Guid ZipId,
    int AddressId,
    DateTimeOffset? OrderDateTime,
    decimal SellCost,
    decimal? PriceCost,
    short? OperationId,
    decimal? Discount,
    decimal? DiscountPercent,
    int UserId,
    short? DeliveryStatusId,
    bool IsPaid = false,
    string? DeliveryCompany = null,
    string? DeliveryComment = null
);

/// <summary>Тело запроса на сохранение черновика формы: JSON со значениями полей.</summary>
public record SaveDraftRequest(string? Content);

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

// ---------- Support ----------
public record CreateSupportTicketRequest(Guid OrderId, string Message);
public record SendSupportMessageRequest(string Text);
public record SupportMessageDto(long Id, bool IsFromAdmin, string AuthorName, string Text, DateTimeOffset CreatedAt);
public record SupportTicketDto(Guid Id, string OrderNumber, Guid OrderId, DateTimeOffset CreatedAt, bool IsClosed, List<SupportMessageDto> Messages);
public record SupportTicketSummaryDto(
    Guid Id, string OrderNumber, DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt, string LastMessagePreview, bool IsClosed,
    string? UserFio = null, string? UserEmail = null);
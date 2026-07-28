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
    string? PartNum,
    string? Mark,
    string? Model,
    string? Group,
    int? Year,
    Guid IncomeMotoId,
    int CountStored);

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
    string? Discount);

public record GuestCreateOrderRequest(
    Guid ZipId,
    int Count,
    string Fio,
    string Email,
    string Phone,
    string? Password,
    string Address,
    string? PostCode,
    string? Promo
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
public record AdminPartNumberRequest(string PartNum);
public record AdminZipRequest(string Name, decimal IncomeCost, int? PartNumId, int? MarkId, int? ModelId, int? GroupId, int CountStored, int? Year, Guid IncomeMotoId);
public record AdminUserRequest(string Email, string FIO, string? PhoneNumber, bool IsAdmin, bool IsRegistrar, bool IsSender, string? Password);
public record AdminAddressRequest(string Address, string? PostCode, int? UserId);
public record AdminOrderRequest(
    string OrderNumber,
    int CountOrdered,
    Guid NomenclatureId,
    int AddressId, // Исправлено на AddressId
    DateTimeOffset? OrderDateTime,
    decimal SellCost,
    short? OperationTypeId,
    decimal? Discount,
    int UserId,
    short? DeliveryStatusId
);

public class UpdateUserRolesRequest
{
    public bool IsSender { get; set; }
    public bool IsRegistrar { get; set; }
}

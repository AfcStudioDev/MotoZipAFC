namespace MotoParts.Domain.Common;

/// <summary>
/// Характер отказа. Нужен, чтобы слой сценариев мог сказать «не найдено» или «нельзя»,
/// не зная про HTTP: перевод в коды состояния делает уже Api (см. ResultExtensions).
/// </summary>
public enum ErrorKind
{
    /// <summary>Данные запроса не проходят проверку — 400.</summary>
    Validation,
    /// <summary>Записи нет — 404.</summary>
    NotFound,
    /// <summary>Конфликт с текущим состоянием: дубль, недостаток товара — 409.</summary>
    Conflict,
    /// <summary>Прав не хватает — 403.</summary>
    Forbidden
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error Validation(string message) => new(ErrorKind.Validation, message);
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
    public static Error Forbidden(string message) => new(ErrorKind.Forbidden, message);
}

/// <summary>
/// Итог операции без возвращаемого значения. Заменяет возврат IActionResult из бизнес-логики:
/// сценарий описывает, что случилось, а не каким должен быть HTTP-ответ.
/// </summary>
public readonly struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static Result Validation(string message) => Failure(Error.Validation(message));
    public static Result NotFound(string message) => Failure(Error.NotFound(message));
    public static Result Conflict(string message) => Failure(Error.Conflict(message));
    public static Result Forbidden(string message) => Failure(Error.Forbidden(message));
}

/// <summary>Итог операции с возвращаемым значением.</summary>
public readonly struct Result<T>
{
    private readonly T _value;

    private Result(T value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    /// <summary>Значение успешной операции. Обращение при отказе — ошибка в коде вызывающего.</summary>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"Результат неуспешен ({Error!.Kind}), значения нет: {Error.Message}");

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default!, error);

    public static Result<T> Validation(string message) => Failure(Error.Validation(message));
    public static Result<T> NotFound(string message) => Failure(Error.NotFound(message));
    public static Result<T> Conflict(string message) => Failure(Error.Conflict(message));
    public static Result<T> Forbidden(string message) => Failure(Error.Forbidden(message));

    /// <summary>Успешный результат можно вернуть просто значением — без Result&lt;T&gt;.Success(...).</summary>
    public static implicit operator Result<T>(T value) => Success(value);
}

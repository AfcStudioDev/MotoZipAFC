using Microsoft.AspNetCore.Mvc;

using MotoParts.Domain.Common;

namespace MotoParts.Api.Common;

/// <summary>
/// Перевод результата сценария в HTTP-ответ. Единственное место, где характер отказа
/// превращается в код состояния, — поэтому Application и Domain про HTTP ничего не знают.
///
/// Тело ошибки везде одинаковое: { "message": "..." }. Фронтенд читает именно
/// err.error?.message, так что менять форму нельзя.
/// </summary>
public static class ResultExtensions
{
    // Именно ObjectResult, а не IActionResult: ActionResult&lt;T&gt; умеет неявно принимать
    // ActionResult, но не интерфейс — иначе методы вида ActionResult&lt;OrderDto&gt; не скомпилируются.
    public static ObjectResult ToErrorResponse(this Error error) => new ObjectResult(new { message = error.Message })
    {
        StatusCode = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        }
    };

    /// <summary>Успех без тела — 200 с коротким сообщением.</summary>
    public static IActionResult ToActionResult(this Result result, string successMessage = "Готово") =>
        result.IsSuccess
            ? new OkObjectResult(new { message = successMessage })
            : result.Error!.ToErrorResponse();

    /// <summary>Успех со значением; onSuccess задаёт форму ответа, которую ждёт фронтенд.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, object> onSuccess) =>
        result.IsSuccess
            ? new OkObjectResult(onSuccess(result.Value))
            : result.Error!.ToErrorResponse();
}

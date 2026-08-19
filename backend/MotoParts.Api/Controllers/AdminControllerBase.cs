using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Security.Claims;

namespace MotoParts.Api.Controllers;

/// <summary>
/// Общее для всех разделов админ-панели: маршрут, роли и текущий пользователь.
///
/// Раньше это был один AdminController на 1376 строк, где справочники, склад, продажи и
/// удаление лежали вперемешку. Разбит по ресурсам; маршруты не изменились — снаружи
/// это по-прежнему api/admin/..., поэтому фронтенд править не пришлось.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Registrar")]
public abstract class AdminControllerBase : ControllerBase
{
    protected int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>
/// Сужает доступ до администратора внутри админ-панели.
///
/// Регистратор заводит новые записи, но не правит и не удаляет уже заведённые, поэтому висит
/// на всех PUT и DELETE в api/admin. Атрибуты авторизации складываются по И: базовый пускает
/// Admin и Registrar, этот оставляет только Admin — снимать базовый не нужно.
///
/// В интерфейсе то же самое: у регистратора спрятана кнопка «Обновить» и погашено «Удалить»,
/// но это лишь удобство — запрет держится здесь.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminOnlyAttribute : AuthorizeAttribute
{
    public AdminOnlyAttribute() => Roles = "Admin";
}

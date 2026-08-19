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

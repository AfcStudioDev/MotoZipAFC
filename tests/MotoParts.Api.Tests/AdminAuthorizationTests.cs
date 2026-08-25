using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MotoParts.Api.Controllers;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Разграничение ролей внутри админ-панели: регистратор заводит новые записи, но не правит
/// и не удаляет уже заведённые.
///
/// Проверяется через отражение, а не вызовом методов: запрет держится на атрибутах авторизации,
/// а их применяет конвейер ASP.NET Core — при прямом вызове метода контроллера он бы не сработал
/// и тест был бы зелёным при дырявом бэкенде. Заодно это ловит главный риск: кто-то добавит
/// новый PUT в api/admin и забудет закрыть его от регистратора.
/// </summary>
public class AdminAuthorizationTests
{
    private static IEnumerable<Type> AdminControllers =>
        typeof(AdminControllerBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(AdminControllerBase).IsAssignableFrom(t));

    /// <summary>Роли из атрибутов метода и его контроллера — они складываются по И.</summary>
    private static IEnumerable<string> RoleRestrictions(MethodInfo method) =>
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(a => a.Roles)
            .Where(r => !string.IsNullOrWhiteSpace(r))!;

    /// <summary>
    /// Пройдёт ли пользователь с единственной ролью <paramref name="role"/> все ограничения метода.
    /// [Authorize] класса и метода складываются по И: у пользователя должна быть роль из
    /// КАЖДОЙ группы, а не хотя бы одной — иначе роль, широкая на уровне метода, не спасает,
    /// если базовый класс её не перечисляет (см. WarehouseCorrectionsController).
    /// </summary>
    private static bool RoleIsAllowed(MethodInfo method, string role) =>
        RoleRestrictions(method).All(group => group.Split(',').Select(r => r.Trim()).Contains(role));

    public static TheoryData<string, string> ИзменяющиеМетодыАдминПанели()
    {
        var data = new TheoryData<string, string>();

        foreach (var controller in AdminControllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var isMutating = method.GetCustomAttributes<HttpPutAttribute>(true).Any()
                    || method.GetCustomAttributes<HttpDeleteAttribute>(true).Any();

                if (isMutating) data.Add(controller.Name, method.Name);
            }
        }

        Assert.NotEmpty(data); // страховка: если отражение перестанет находить методы, тест не должен молча пройти
        return data;
    }

    [Theory]
    [MemberData(nameof(ИзменяющиеМетодыАдминПанели))]
    public void Правка_и_удаление_в_админ_панели_закрыты_от_регистратора(string controllerName, string methodName)
    {
        var controller = AdminControllers.Single(t => t.Name == controllerName);
        var method = controller.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

        var restrictions = RoleRestrictions(method).ToList();

        Assert.True(restrictions.Any(r => r == "Admin"),
            $"{controllerName}.{methodName} меняет или удаляет данные, но доступен не только администратору. " +
            $"Навесьте [AdminOnly]. Текущие ограничения: {string.Join(" И ", restrictions)}");
    }

    [Fact]
    public void Создание_записей_регистратору_по_прежнему_доступно()
    {
        // Обратная сторона: если закрыть от регистратора ещё и POST, роль потеряет смысл.
        var addOrder = typeof(AdminSalesController)
            .GetMethod(nameof(AdminSalesController.AddOrder), BindingFlags.Public | BindingFlags.Instance)!;

        Assert.DoesNotContain("Admin", RoleRestrictions(addOrder));
    }

    /// <summary>
    /// Коррекция остатка и переоценка живут в WarehouseCorrectionsController — отдельно от
    /// AdminWarehouseController — именно потому, что нужны Sender-у наравне с Admin, но не
    /// Registrar-у. Открыть их Sender-у методом-уровневым [Authorize(Roles="Admin,Sender")]
    /// прямо в AdminWarehouseController нельзя: он всё равно комбинировался бы по И с
    /// [Authorize(Roles="Admin,Registrar")] базового класса, и Sender так и остался бы снаружи.
    /// </summary>
    [Theory]
    [InlineData(nameof(WarehouseCorrectionsController.AddCorrection))]
    [InlineData(nameof(WarehouseCorrectionsController.Reprice))]
    public void Коррекция_остатка_и_переоценка_доступны_Admin_и_Sender_но_не_Registrar(string methodName)
    {
        var method = typeof(WarehouseCorrectionsController)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.True(RoleIsAllowed(method, "Admin"), $"{methodName} должен быть доступен Admin");
        Assert.True(RoleIsAllowed(method, "Sender"), $"{methodName} должен быть доступен Sender");
        Assert.False(RoleIsAllowed(method, "Registrar"), $"{methodName} не должен быть доступен Registrar");
    }
}

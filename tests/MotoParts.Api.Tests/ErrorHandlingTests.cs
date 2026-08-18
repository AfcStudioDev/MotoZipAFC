using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using MotoParts.Api.Common;
using MotoParts.Domain.Common;

using System.Text.Json;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Перевод отказов в HTTP. Форма тела ошибки — часть контракта с фронтендом: он читает
/// err.error?.message, поэтому во всех ответах должно быть поле message.
/// </summary>
public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    public void Характер_отказа_определяет_код_состояния(ErrorKind kind, int expectedStatus)
    {
        var response = new Error(kind, "текст ошибки").ToErrorResponse();

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public void Тело_ошибки_всегда_содержит_message()
    {
        var response = Error.Conflict("Недостаточно товара").ToErrorResponse();

        var json = JsonSerializer.Serialize(response.Value);
        using var parsed = JsonDocument.Parse(json);

        Assert.True(parsed.RootElement.TryGetProperty("message", out var message),
            "Фронтенд читает err.error?.message — без этого поля он покажет пустую ошибку");
        Assert.Equal("Недостаточно товара", message.GetString());
    }

    [Fact]
    public void Успешный_результат_отдаёт_значение_в_заданной_форме()
    {
        Result<int> result = 42;

        var response = Assert.IsType<OkObjectResult>(result.ToActionResult(count => new { count }));

        var json = JsonSerializer.Serialize(response.Value);
        Assert.Contains("\"count\":42", json);
    }

    [Fact]
    public void Обращение_к_значению_неуспешного_результата_не_молчит()
    {
        // Лучше упасть на месте, чем тихо вернуть default и записать 0 в остаток.
        var result = Result<int>.Conflict("нельзя");

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}

/// <summary>
/// Раньше исключения ловили точечно в контроллерах и возвращали наружу ex.InnerException?.Message —
/// то есть текст ошибки БД с именами таблиц и ограничений. Теперь это делает middleware.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, string Body)> InvokeWith(RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task Необработанное_исключение_превращается_в_500_без_подробностей()
    {
        var (status, body) = await InvokeWith(_ =>
            throw new InvalidOperationException(
                "23503: insert or update on table \"Orders\" violates foreign key constraint \"FK_Orders_Users_UserId\""));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);

        // Ни текста исключения, ни имён таблиц и ограничений наружу уходить не должно.
        Assert.DoesNotContain("FK_Orders", body);
        Assert.DoesNotContain("constraint", body);
        Assert.DoesNotContain("23503", body);

        using var parsed = JsonDocument.Parse(body);
        Assert.True(parsed.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task Успешный_запрос_middleware_не_трогает()
    {
        var (status, body) = await InvokeWith(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsync("{\"ok\":true}");
        });

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("{\"ok\":true}", body);
    }
}

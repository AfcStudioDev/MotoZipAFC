using System.Text.Json;

namespace MotoParts.Api.Common;

/// <summary>
/// Единая обработка необработанных исключений. Раньше их ловили точечно в контроллерах и
/// возвращали 500 с ex.InnerException?.Message — то есть наружу уезжал текст ошибки БД
/// (имена таблиц, ограничений, иногда данные). Теперь клиент получает нейтральное сообщение
/// в том же виде { "message": "..." }, что и остальные ошибки, а подробности уходят в лог.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Необработанная ошибка при {Method} {Path}", context.Request.Method, context.Request.Path);

            // Ответ мог уже начать отправляться — тогда менять статус поздно, только обрывать.
            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = "Внутренняя ошибка сервера. Попробуйте повторить действие; если повторится — обратитесь к администратору."
            }));
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}

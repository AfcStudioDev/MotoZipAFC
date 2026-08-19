using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MotoParts.Application.Abstractions;
using MotoParts.Application.Warehouse;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Infrastructure.Services;

using VkChatBot;

namespace MotoParts.Infrastructure;

/// <summary>
/// Регистрация инфраструктуры одной строкой из Program.cs. Так host знает, что подключить,
/// но не знает, чем именно реализованы интерфейсы Application, — а состав пакетов
/// (Npgsql, MailKit, ImageSharp) остаётся внутри этого слоя.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        // Сценарии работают с интерфейсом, а получают тот же экземпляр AppDbContext,
        // что и остальной запрос, — иначе изменения ушли бы в разные транзакции.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHttpClient();

        services.AddScoped<TokenService>();
        services.AddScoped<OAuthService>();
        services.AddScoped<YooKassaService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IVkBotService, VkBotService>();

        return services;
    }

    /// <summary>Сценарии предметной области. Отдельным методом, чтобы слои не смешивались в Program.cs.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<WarehouseService>();
        return services;
    }
}

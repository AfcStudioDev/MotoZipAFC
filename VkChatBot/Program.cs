using Microsoft.Extensions.Configuration;

using VkBotFramework;
using VkBotFramework.Models;

namespace VkChatBot;

/// <summary>
/// Отдельный консольный запуск — только для локальной отладки long-poll слушателя бота.
/// В продакшене VkChatBot используется как библиотека (см. VkBotService, регистрируется
/// в MotoParts.Infrastructure.DependencyInjection) и к отправке сообщений при оформлении
/// заказа этот класс отношения не имеет.
///
/// Конфигурация берётся из переменных окружения (VkBot__AccessToken, VkBot__GroupUrl) или
/// необязательного appsettings.local.json рядом с exe — оба варианта не хранят секрет в коде.
/// </summary>
public static class Program
{
    private static void Main()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var accessToken = configuration["VkBot:AccessToken"]
            ?? throw new InvalidOperationException("Задайте VkBot__AccessToken в переменных окружения");
        var groupUrl = configuration["VkBot:GroupUrl"]
            ?? throw new InvalidOperationException("Задайте VkBot__GroupUrl в переменных окружения");

        var bot = new VkBot(accessToken, groupUrl);

        Console.WriteLine("Инициализация бота...");
        Console.WriteLine("Подписка на события...");
        bot.OnMessageReceived += OnMessageReceived;

        Task.Run(() =>
        {
            Console.WriteLine("Запуск бота в фоне...");
            bot.Start();
        });

        Console.WriteLine("Бот запущен и ожидает сообщения...");
        Console.WriteLine("Нажмите Enter для выхода\n");
        Console.ReadLine();
    }

    private static void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        Console.WriteLine("=================================");
        Console.WriteLine("Получено сообщение!");
        Console.WriteLine($"От: {e.Message.FromId}");
        Console.WriteLine($"Текст: {e.Message.Text}");
        Console.WriteLine($"PeerId: {e.Message.PeerId}");
        Console.WriteLine($"Время: {e.Message.Date}");
        Console.WriteLine("=================================");
    }
}

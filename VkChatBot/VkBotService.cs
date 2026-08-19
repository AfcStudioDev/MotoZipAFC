using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using VkBotFramework;
using VkNet.Model.RequestParams;

namespace VkChatBot;

/// <summary>
/// Отправка сообщений через VK-бота. Токен, адрес группы и список получателей (PeerId)
/// берутся из конфигурации (секция VkBot), а не из кода — раньше они были захардкожены
/// прямо в исходниках вместе с боевым токеном.
///
/// PeerIds можно задать двумя способами:
///   - массивом в appsettings.json:           "VkBot": { "PeerIds": [111, 222] }
///   - одной строкой через запятую в переменной окружения: VkBot__PeerIds=111,222
/// (второй вариант проще прокидывать через docker-compose, где нет обычных индексов массива).
///
/// Уведомление в VK — не обязательное условие оформления заказа. Раньше отсутствие
/// настроек валило конструктор с исключением, а его читает DI при создании
/// PurchasesController/SupportController — то есть без VK ломались вообще все покупки
/// и обращения в поддержку, даже никак не связанные с уведомлениями. Теперь при
/// отсутствии настроек сервис просто не отправляет сообщения (как EmailService
/// без настроенного SMTP), а сбой самой отправки не мешает уже сохранённой покупке.
/// </summary>
public class VkBotService : IVkBotService
{
    private readonly ILogger<VkBotService> _logger;
    private readonly VkBot? _bot;
    private readonly IReadOnlyList<long> _peerIds;

    public VkBotService(IConfiguration configuration, ILogger<VkBotService> logger)
    {
        _logger = logger;

        var accessToken = configuration["VkBot:AccessToken"];
        var groupUrl = configuration["VkBot:GroupUrl"];
        _peerIds = ParsePeerIds(configuration);

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(groupUrl) || _peerIds.Count == 0)
        {
            _logger.LogInformation("VkBot не настроен (VkBot:AccessToken/GroupUrl/PeerIds) — уведомления в VK отправляться не будут");
            return;
        }

        _bot = new VkBot(accessToken, groupUrl);
    }

    private static IReadOnlyList<long> ParsePeerIds(IConfiguration configuration)
    {
        // Плоское значение — это переменная окружения вида VkBot__PeerIds=111,222.
        var flat = configuration["VkBot:PeerIds"];
        if (!string.IsNullOrWhiteSpace(flat))
        {
            return flat
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(long.Parse)
                .ToList();
        }

        // Иначе — обычный JSON-массив из appsettings.json.
        return configuration.GetSection("VkBot:PeerIds").Get<long[]>() ?? Array.Empty<long>();
    }

    public void SendMessage(string message)
    {
        if (_bot == null) return;

        // Покупка/обращение уже сохранены к этому моменту — сбой уведомления
        // (сеть, невалидный токен, VK недоступен) не должен превращать успешный
        // запрос пользователя в 500.
        try
        {
            foreach (var peerId in _peerIds)
            {
                _bot.Api.Messages.Send(new MessagesSendParams
                {
                    Message = message,
                    PeerId = peerId,
                    RandomId = Environment.TickCount
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось отправить уведомление в VK");
        }
    }
}

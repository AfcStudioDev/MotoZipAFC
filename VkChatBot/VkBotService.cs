using Microsoft.Extensions.Configuration;

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
/// </summary>
public class VkBotService : IVkBotService
{
    private readonly VkBot _bot;
    private readonly IReadOnlyList<long> _peerIds;

    public VkBotService(IConfiguration configuration)
    {
        var accessToken = configuration["VkBot:AccessToken"];
        var groupUrl = configuration["VkBot:GroupUrl"];

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("VkBot:AccessToken не задан в конфигурации");
        if (string.IsNullOrWhiteSpace(groupUrl))
            throw new InvalidOperationException("VkBot:GroupUrl не задан в конфигурации");

        _peerIds = ParsePeerIds(configuration);
        if (_peerIds.Count == 0)
            throw new InvalidOperationException("VkBot:PeerIds не задан — некому отправлять сообщения");

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
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using VkChatBot;

using Xunit;

namespace MotoParts.Api.Tests;

/// <summary>
/// Регрессия: VkBotService раньше бросал исключение прямо из конструктора, если
/// VkBot:AccessToken/GroupUrl/PeerIds не заданы. Конструктор читает DI при создании
/// PurchasesController и SupportController — значит, без настроенного VK ломались бы
/// вообще все покупки и обращения в поддержку, хотя к уведомлениям в VK они прямого
/// отношения не имеют. См. appsettings.json (VkBot по умолчанию пустой).
/// </summary>
public class VkBotServiceTests
{
    [Fact]
    public void Без_настроек_конструктор_не_падает()
    {
        var configuration = new ConfigurationBuilder().Build();

        var ex = Record.Exception(() => new VkBotService(configuration, NullLogger<VkBotService>.Instance));

        Assert.Null(ex);
    }

    [Fact]
    public void Без_настроек_отправка_сообщения_не_падает()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new VkBotService(configuration, NullLogger<VkBotService>.Instance);

        var ex = Record.Exception(() => service.SendMessage("тест"));

        Assert.Null(ex);
    }
}

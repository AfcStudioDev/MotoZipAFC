namespace VkChatBot;

public interface IVkBotService
{
    /// <summary>Отправляет сообщение во все настроенные получатели (VkBot:PeerIds).</summary>
    void SendMessage(string message);
}

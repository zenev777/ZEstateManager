using ZEstate.Core.DTOs.Chat;

namespace ZEstate.Core.Interfaces
{
    public record ChatMessageDeleted(int BuildingId);
    public record ChatMessageSent(ChatMessageResponseDto Message, int BuildingId);

    public interface IChatService
    {
        Task<List<ChatMessageResponseDto>> GetMessagesAsync(string userId);
        Task<ChatMessageSent> SendMessageAsync(string userId, string message);
        Task<ChatMessageDeleted> DeleteMessageAsync(string managerId, int messageId);
    }
}

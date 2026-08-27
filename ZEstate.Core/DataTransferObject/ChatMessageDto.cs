// ChatMessageDto.cs
namespace ZEstate.Core.DTOs.Chat;

public class ChatMessageResponseDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
}

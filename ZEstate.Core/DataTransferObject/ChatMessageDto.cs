// ChatMessageDto.cs
namespace ZEstate.Core.DTOs.Chat;

public class ChatMessageResponseDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    // Primary Identity role of the sender (e.g. "HouseManager") - the client
    // translates it to the Bulgarian label shown next to the name.
    public string SenderRole { get; set; } = string.Empty;
}

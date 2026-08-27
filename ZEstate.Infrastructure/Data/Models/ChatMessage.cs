using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models;

public class ChatMessage
{
    public const int MessageMaxLength = 1000;

    [Key]
    [Comment("Chat message identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Building identifier - the chat is one shared channel per building")]
    public int BuildingId { get; set; }

    [Required]
    [ForeignKey(nameof(BuildingId))]
    public Building Building { get; set; } = null!;

    [Required]
    [Comment("Sender user identifier")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(MessageMaxLength)]
    [Comment("Message text")]
    public string Message { get; set; } = string.Empty;

    [Required]
    [Comment("Date/time the message was sent")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models;

public class Notification
{
    [Key]
    [Comment("Notification identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Recipient user identifier")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    [Comment("Short notification title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Comment("Notification body")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    [Comment("Optional in-app deep link related to the notification")]
    public string? Link { get; set; }

    [Required]
    [Comment("Whether the recipient has read the notification")]
    public bool IsRead { get; set; } = false;

    [Required]
    [Comment("Date/time the notification was created")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

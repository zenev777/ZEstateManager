using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models;

public class InviteCodeLog
{
    [Key]
    [Comment("Invite code log entry identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Building identifier")]
    public int BuildingId { get; set; }

    [Required]
    [ForeignKey(nameof(BuildingId))]
    public Building Building { get; set; } = null!;

    [Required]
    [Comment("User identifier of the house manager who made the change")]
    public string ChangedByUserId { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(ChangedByUserId))]
    public ApplicationUser ChangedBy { get; set; } = null!;

    [Required]
    [Comment("What kind of change was made to the invite code")]
    public InviteCodeAction Action { get; set; }

    [MaxLength(10)]
    [Comment("The invite code before the change, if applicable")]
    public string? OldCode { get; set; }

    [MaxLength(10)]
    [Comment("The invite code after the change, if applicable")]
    public string? NewCode { get; set; }

    [Required]
    [Comment("Date/time the change was made")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

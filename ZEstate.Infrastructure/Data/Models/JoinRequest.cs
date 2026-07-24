using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models;

public class JoinRequest
{
    public const int NotesMaxLength = 300;

    [Key]
    [Comment("Join request identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Building identifier")]
    public int BuildingId { get; set; }

    [Required]
    [ForeignKey(nameof(BuildingId))]
    public Building Building { get; set; } = null!;

    [Required]
    [Comment("User identifier")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [Comment("Apartment identifier the user wants to join")]
    public int ApartmentId { get; set; }

    [Required]
    [ForeignKey(nameof(ApartmentId))]
    public Apartment Apartment { get; set; } = null!;

    [Required]
    [Comment("Current status of the join request")]
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

    [Required]
    [Comment("Role the user requested when joining (owner or resident/tenant)")]
    public ApartmentRole RequestedRole { get; set; } = ApartmentRole.Resident;

    [MaxLength(NotesMaxLength)]
    [Comment("Optional note from the user")]
    public string? Notes { get; set; }

    [Required]
    [Comment("Date when the request was submitted")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Comment("Date when the request was reviewed")]
    public DateTime? ReviewedAt { get; set; }
}
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models;

public class ApartmentTransferLog
{
    [Key]
    [Comment("Apartment transfer log entry identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Apartment identifier")]
    public int ApartmentId { get; set; }

    [Required]
    [ForeignKey(nameof(ApartmentId))]
    public Apartment Apartment { get; set; } = null!;

    [Comment("User identifier of the owner who left, if there was an active one")]
    public string? PreviousOwnerUserId { get; set; }

    [ForeignKey(nameof(PreviousOwnerUserId))]
    public ApplicationUser? PreviousOwner { get; set; }

    [Required]
    [Comment("User identifier of the house manager who performed the transfer")]
    public string TransferredByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(TransferredByUserId))]
    public ApplicationUser TransferredBy { get; set; } = null!;

    [Required]
    [Comment("How outstanding debts at the time of transfer were handled")]
    public DebtHandling DebtHandling { get; set; }

    [Required]
    [Comment("Sum of outstanding obligation balances at the moment of transfer, for audit")]
    public decimal OutstandingBalanceAtTransfer { get; set; }

    [Required]
    [Comment("Date/time the transfer was recorded")]
    public DateTime TransferredAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ZEstate.Infrastructure.Data.Enums;

namespace ZEstate.Infrastructure.Data.Models
{
    // A single signed movement into or out of one of the building's two cash accounts
    // (В брой / По банка). The balance of an account is the sum of its entries.
    // A resident's Payment produces one entry (account decided by PaymentMethod);
    // an internal transfer between the two accounts produces two linked entries
    // (one negative, one positive) sharing the same TransferGroupId.
    public class CashLedgerEntry
    {
        [Key]
        [Comment("Cash ledger entry identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public int BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Required]
        [Comment("Which account (Cash or Bank) this entry affects")]
        public CashAccountType Account { get; set; }

        [Required]
        [Comment("Signed amount - positive increases the account balance, negative decreases it")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(300)]
        [Comment("Human-readable description of the movement")]
        public string Description { get; set; } = string.Empty;

        [Comment("The resident payment that produced this entry, if any")]
        public int? PaymentId { get; set; }

        [ForeignKey(nameof(PaymentId))]
        public Payment? Payment { get; set; }

        [Comment("Links the two legs of one internal transfer between accounts")]
        public Guid? TransferGroupId { get; set; }

        [Comment("Who recorded this entry (null for entries produced automatically, e.g. Stripe webhook)")]
        public string? CreatedByUserId { get; set; }

        [Required]
        [Comment("Date and time the entry was recorded")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

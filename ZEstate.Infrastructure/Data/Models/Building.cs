using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
using System.Text;
using ZEstate.Infrastructure.Data.IdentityModels;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Building
    {
        [Key]
        [Comment("Building identifier")]
        public int Id { get; set; }

        [Required]
        [MaxLength(NameMaxLength)]
        [Comment("Building name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(AddressMaxLength)]
        [Comment("Building address")]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Comment("Date when the building was added to the system")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(10)]
        [Comment("Unique invite code for residents to join")]
        public string InviteCode { get; set; } = string.Empty;

        [Required]
        [Comment("Whether the invite code currently accepts new registrations")]
        public bool InviteCodeActive { get; set; } = true;

        [Comment("Optional expiration date/time for the invite code")]
        public DateTime? InviteCodeExpiresAt { get; set; }

        [Comment("Optional maximum number of times the invite code can be used")]
        public int? InviteCodeMaxUses { get; set; }

        [Required]
        [Comment("Number of times the current invite code has been used")]
        public int InviteCodeUseCount { get; set; } = 0;

        [Required]
        [Comment("Minimum percentage of ideal parts that must vote for a decision to have quorum (ЗУЕС default: 50)")]
        public decimal QuorumThresholdPercent { get; set; } = 50;

        [Comment("User identifier of the pending HouseManager successor, if a transfer is in progress")]
        public string? PendingManagerTransferToUserId { get; set; }

        [Comment("When the pending manager transfer was initiated")]
        public DateTime? PendingManagerTransferInitiatedAt { get; set; }

        [Comment("When the pending manager transfer takes effect, unless cancelled first")]
        public DateTime? PendingManagerTransferEffectiveAt { get; set; }

        [MaxLength(34)]
        [Comment("IBAN the building receives online (Stripe) payments' payouts to - required before online payment can be offered to residents")]
        public string? Iban { get; set; }

        [Comment("The house manager responsible for this building")]
        public string? ManagerId { get; set; }

        [ForeignKey(nameof(ManagerId))]
        public ApplicationUser? Manager { get; set; }

        public ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
        public ICollection<Fee> Fees { get; set; } = new List<Fee>();
        public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
        public ICollection<Repair> Repairs { get; set; } = new List<Repair>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<InviteCodeLog> InviteCodeLogs { get; set; } = new List<InviteCodeLog>();
    }
}

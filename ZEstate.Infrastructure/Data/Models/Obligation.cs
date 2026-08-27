using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Data.Models
{

    public class Obligation
    {
        [Key]
        [Comment("Obligation identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Apartment identifier")]
        public int ApartmentId { get; set; }

        [Required]
        [ForeignKey(nameof(ApartmentId))]
        public Apartment Apartment { get; set; } = null!;

        [Required]
        [Comment("Fee identifier")]
        public int FeeId { get; set; }

        [Required]
        [ForeignKey(nameof(FeeId))]
        public Fee Fee { get; set; } = null!;

        [Required]
        [Comment("Amount due")]
        public decimal Amount { get; set; }

        [Required]
        [Comment("Current payment status")]
        public ObligationStatus Status { get; set; } = ObligationStatus.Pending;

        [Required]
        [Comment("Date when the obligation was created")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Comment("Due date for the obligation")]
        public DateTime? DueDate { get; set; }

        [Comment("First day of the month this obligation was generated for (Monthly fees only, used to prevent duplicate generation); null for OneTime fees")]
        public DateTime? Period { get; set; }

        [Comment("Set at apartment-transfer time if the manager chose to keep this debt with the departing owner rather than passing it to the new one")]
        public string? PreviousOwnerUserId { get; set; }

        [ForeignKey(nameof(PreviousOwnerUserId))]
        public ApplicationUser? PreviousOwner { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

}

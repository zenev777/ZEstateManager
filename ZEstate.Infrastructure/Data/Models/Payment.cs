using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Payment
    {
        [Key]
        [Comment("Payment identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Obligation identifier")]
        public int ObligationId { get; set; }

        [Required]
        [ForeignKey(nameof(ObligationId))]
        public Obligation Obligation { get; set; } = null!;

        [Required]
        [Comment("Amount paid")]
        public decimal Amount { get; set; }

        [Required]
        [Comment("Date and time of payment")]
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Comment("Payment method used")]
        public PaymentMethod Method { get; set; } = PaymentMethod.Manual;

        [MaxLength(TransactionIdMaxLength)]
        [Comment("Stripe transaction identifier, null if paid manually")]
        public string? TransactionId { get; set; }

        [MaxLength(300)]
        [Comment("Optional note entered when recording the payment")]
        public string? Note { get; set; }
    }
}

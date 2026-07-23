using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.AccessControl;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Fee
    {
        [Key]
        [Comment("Fee identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public int BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Comment("Repair identifier, if fee is related to a repair")]
        public int? RepairId { get; set; }

        [ForeignKey(nameof(RepairId))]
        public Repair? Repair { get; set; }

        [Required]
        [MaxLength(TitleMaxLength)]
        [Comment("Fee title")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(DescriptionMaxLength)]
        [Comment("Fee description")]
        public string? Description { get; set; }

        [Required]
        [Comment("Fee amount")]
        public decimal Amount { get; set; }

        [Required]
        [Comment("Fee type: Fixed, PerIdealPart or Repair")]
        public FeeType Type { get; set; }

        [Required]
        [Comment("Date from which the fee is active")]
        public DateTime DateFrom { get; set; }

        [Comment("Date until which the fee is active")]
        public DateTime? DateTo { get; set; }

        [Required]
        [Comment("Fee priority")]
        public FeePriority Priority { get; set; } = FeePriority.Normal;

        [Required]
        [Comment("Date when the fee was created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Obligation> Obligations { get; set; } = new List<Obligation>();
    }
}

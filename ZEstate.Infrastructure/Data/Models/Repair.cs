using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Repair
    {
        [Key]
        [Comment("Repair identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public int BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Required]
        [MaxLength(TitleMaxLength)]
        [Comment("Repair title")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(DescriptionMaxLength)]
        [Comment("Repair description")]
        public string? Description { get; set; }

        [Required]
        [Comment("Repair budget")]
        public decimal Budget { get; set; }

        [Required]
        [Comment("Current repair status")]
        public RepairStatus Status { get; set; } = RepairStatus.Planned;

        [Required]
        [Comment("Date when the repair was created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Fee> Fees { get; set; } = new List<Fee>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }

}

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Xml.Linq;
using ZEstate.Infrastructure.Data.Enums;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Document
    {
        [Key]
        [Comment("Document identifier")]
        public Guid Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public Guid BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Comment("Repair identifier, if document belongs to a repair")]
        public Guid? RepairId { get; set; }

        [ForeignKey(nameof(RepairId))]
        public Repair? Repair { get; set; }

        [Required]
        [MaxLength(FilePathMaxLength)]
        [Comment("File storage path")]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(FileNameMaxLength)]
        [Comment("Original file name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Comment("Document type: Protocol, Contract, Invoice or Other")]
        public DocumentType Type { get; set; }

        [Required]
        [Comment("Access level: All or ManagerOnly")]
        public DocumentAccess Access { get; set; } = DocumentAccess.All;

        [Required]
        [Comment("Date when the document was uploaded")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}

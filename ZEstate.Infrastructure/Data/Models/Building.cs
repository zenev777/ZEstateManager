using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;
using System.Text;
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

        public ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
        public ICollection<Fee> Fees { get; set; } = new List<Fee>();
        public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
        public ICollection<Repair> Repairs { get; set; } = new List<Repair>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}

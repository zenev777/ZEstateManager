using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static ZEstate.Infrastructure.Data.DataConstants.DataConstants;
using System.Text;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Apartment
    {
        [Key]
        [Comment("Apartment identifier")]
        public Guid Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public Guid BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Required]
        [MaxLength(NumberMaxLength)]
        [Comment("Apartment number")]
        public string Number { get; set; } = string.Empty;

        [Required]
        [Comment("Floor number")]
        public int Floor { get; set; }

        [Required]
        [Comment("Ideal parts percentage of the building")]
        public decimal IdealParts { get; set; }

        [Required]
        [Comment("Apartment budget balance")]
        public decimal Budget { get; set; }

        public ICollection<ApartmentUser> ApartmentUsers { get; set; } = new List<ApartmentUser>();
        public ICollection<Obligation> Obligations { get; set; } = new List<Obligation>();
    }
}

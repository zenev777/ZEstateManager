using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;

namespace ZEstate.Infrastructure.Data.Models
{
    public class ApartmentUser
    {
        [Required]
        [Comment("Apartment identifier")]
        public Guid ApartmentId { get; set; }

        [Required]
        [ForeignKey(nameof(ApartmentId))]
        public Apartment Apartment { get; set; } = null!;

        [Required]
        [Comment("User identifier")]
        public Guid UserId { get; set; }

        [Required]
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [Comment("Role of the user in this apartment")]
        public ApartmentRole Role { get; set; } = ApartmentRole.Resident;

        [Required]
        [Comment("Whether the user is currently active in this apartment")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Comment("Date when the user joined the apartment")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static ZEstate.Infrastructure.Data.DataConstants.IdentityConstants;

namespace ZEstate.Infrastructure.Data.Models
{
    public class User : IdentityUser<Guid>
    {
        [Required]
        [MaxLength(UserNamesMaxLenght)]
        [Comment("Full name of the user")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Comment("Date when the user registered")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Comment("Whether the user account is active")]
        public bool IsActive { get; set; } = true;

        public ICollection<ApartmentUser> ApartmentUsers { get; set; } = new List<ApartmentUser>();
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}

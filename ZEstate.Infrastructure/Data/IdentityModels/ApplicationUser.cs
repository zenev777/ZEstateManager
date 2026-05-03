using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static ZEstate.Infrastructure.Data.DataConstants.IdentityConstants;

namespace ZEstate.Infrastructure.Data.IdentityModels
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(UserNamesMaxLenght)]
        public string? FirstName { get; set; }

        [StringLength(UserNamesMaxLenght)]
        public string? LastName { get; set; }
    }
}

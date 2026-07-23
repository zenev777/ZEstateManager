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
    public class Vote
    {
        [Key]
        [Comment("Vote identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Meeting identifier")]
        public int MeetingId { get; set; }

        [Required]
        [ForeignKey(nameof(MeetingId))]
        public Meeting Meeting { get; set; } = null!;

        [Required]
        [Comment("User identifier")]
        public string UserId { get; set; }

        [Required]
        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        [Comment("Vote value: Yes, No or Abstain")]
        public VoteValue Value { get; set; }

        [Required]
        [Comment("Date and time when the vote was cast")]
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    }
}

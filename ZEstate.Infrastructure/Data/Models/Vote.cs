using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Vote
    {
        [Key]
        [Comment("Vote identifier")]
        public Guid Id { get; set; }

        [Required]
        [Comment("Meeting identifier")]
        public Guid MeetingId { get; set; }

        [Required]
        [ForeignKey(nameof(MeetingId))]
        public Meeting Meeting { get; set; } = null!;

        [Required]
        [Comment("User identifier")]
        public Guid UserId { get; set; }

        [Required]
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [Comment("Vote value: Yes, No or Abstain")]
        public VoteValue Value { get; set; }

        [Required]
        [Comment("Date and time when the vote was cast")]
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    }
}

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        [Comment("Vote question identifier")]
        public int VoteQuestionId { get; set; }

        [Required]
        [ForeignKey(nameof(VoteQuestionId))]
        public VoteQuestion VoteQuestion { get; set; } = null!;

        // One vote per apartment per question (ideal parts is the apartment's weight,
        // not the casting user's) - see the unique index in ApplicationDbContext.
        [Required]
        [Comment("Apartment identifier this vote is cast on behalf of")]
        public int ApartmentId { get; set; }

        [Required]
        [ForeignKey(nameof(ApartmentId))]
        public Apartment Apartment { get; set; } = null!;

        [Required]
        [Comment("User identifier of whoever cast the vote")]
        public string UserId { get; set; } = string.Empty;

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

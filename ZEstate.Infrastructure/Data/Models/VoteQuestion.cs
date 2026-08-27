using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZEstate.Infrastructure.Data.Models;

public class VoteQuestion
{
    public const int QuestionMaxLength = 300;

    [Key]
    [Comment("Vote question identifier")]
    public int Id { get; set; }

    [Required]
    [Comment("Meeting identifier")]
    public int MeetingId { get; set; }

    [Required]
    [ForeignKey(nameof(MeetingId))]
    public Meeting Meeting { get; set; } = null!;

    [Required]
    [MaxLength(QuestionMaxLength)]
    [Comment("The question being voted on")]
    public string Question { get; set; } = string.Empty;

    [Required]
    [Comment("When voting opens")]
    public DateTime StartAt { get; set; }

    [Required]
    [Comment("When voting closes")]
    public DateTime EndAt { get; set; }

    [Required]
    [Comment("Date when the question was created")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

// VotingDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Voting;

public class CreateVoteQuestionDto
{
    [Required]
    [MaxLength(300)]
    public string Question { get; set; } = string.Empty;

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }
}

public class CastVoteDto
{
    // "Yes" | "No" | "Abstain"
    [Required]
    public string Value { get; set; } = string.Empty;
}

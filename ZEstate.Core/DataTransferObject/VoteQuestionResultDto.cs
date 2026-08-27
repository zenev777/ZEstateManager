// VoteQuestionResultDto.cs
namespace ZEstate.Core.DTOs.Voting;

public class VoteTallyDto
{
    public decimal YesWeight { get; set; }
    public decimal NoWeight { get; set; }
    public decimal AbstainWeight { get; set; }
    public decimal VotedWeight { get; set; }
    public decimal TotalIdealParts { get; set; }
    public decimal YesPercent { get; set; }
    public decimal NoPercent { get; set; }
    public decimal AbstainPercent { get; set; }
    public decimal TurnoutPercent { get; set; }
    public decimal QuorumThresholdPercent { get; set; }
    public bool QuorumMet { get; set; }

    // Only meaningful once voting has actually closed - regardless of the Yes/No
    // split, a Closed question without quorum is void per ЗУЕС.
    public bool? IsValid { get; set; }
}

public class VoteQuestionResultDto
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    // "Scheduled" | "Open" | "Closed" - a computed label, not a persisted enum.
    public string Status { get; set; } = string.Empty;
    public bool HasVoted { get; set; }
    public VoteTallyDto Result { get; set; } = new();
}

public class VoteHistoryEntryDto
{
    public string MeetingTitle { get; set; } = string.Empty;
    public VoteQuestionResultDto Question { get; set; } = new();
}

// MeetingResponseDto.cs
namespace ZEstate.Core.DTOs.Meetings;

public class MeetingResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Agenda { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string? MeetUrl { get; set; }

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.MeetingStatus.
    public int Status { get; set; }
}

public class MeetingMinutesDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

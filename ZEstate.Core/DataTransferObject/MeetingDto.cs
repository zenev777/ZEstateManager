// MeetingDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Meetings;

public class CreateMeetingDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(300)]
    public string? MeetUrl { get; set; }
}

public class UpdateMeetingDto : CreateMeetingDto
{
    // "Upcoming" | "Active" | "Closed"
    [Required]
    public string Status { get; set; } = "Upcoming";
}

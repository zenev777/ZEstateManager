using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ZEstate.Infrastructure.Data.Enums;

namespace ZEstate.Infrastructure.Data.Models
{
    public class Meeting
    {
        public const int TitleMaxLength = 150;
        public const int DescriptionMaxLength = 1000;
        public const int MeetUrlMaxLength = 300;
        public const int AgendaMaxLength = 2000;
        public const int LocationMaxLength = 200;

        [Key]
        [Comment("Meeting identifier")]
        public int Id { get; set; }

        [Required]
        [Comment("Building identifier")]
        public int BuildingId { get; set; }

        [Required]
        [ForeignKey(nameof(BuildingId))]
        public Building Building { get; set; } = null!;

        [Required]
        [MaxLength(TitleMaxLength)]
        [Comment("Meeting title")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(DescriptionMaxLength)]
        [Comment("Meeting description")]
        public string? Description { get; set; }

        [MaxLength(AgendaMaxLength)]
        [Comment("Agenda items, one per line")]
        public string? Agenda { get; set; }

        [Required]
        [Comment("Meeting start date and time")]
        public DateTime StartDate { get; set; }

        [Required]
        [Comment("Meeting end date and time")]
        public DateTime EndDate { get; set; }

        [MaxLength(LocationMaxLength)]
        [Comment("Physical location, if not (or in addition to) a video link")]
        public string? Location { get; set; }

        [MaxLength(MeetUrlMaxLength)]
        [Comment("Google Meet link for the meeting")]
        public string? MeetUrl { get; set; }

        [Required]
        [Comment("Current meeting status")]
        public MeetingStatus Status { get; set; } = MeetingStatus.Upcoming;

        public ICollection<VoteQuestion> VoteQuestions { get; set; } = new List<VoteQuestion>();
    }
}

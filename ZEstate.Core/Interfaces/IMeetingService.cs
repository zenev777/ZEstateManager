using ZEstate.Core.DTOs.Meetings;

namespace ZEstate.Core.Interfaces
{
    public interface IMeetingService
    {
        Task<List<MeetingResponseDto>> GetMeetingsAsync(string userId);
        Task<MeetingResponseDto> CreateMeetingAsync(string managerId, CreateMeetingDto dto);
        Task<MeetingResponseDto> UpdateMeetingAsync(string managerId, int meetingId, UpdateMeetingDto dto);
        Task DeleteMeetingAsync(string managerId, int meetingId);
        string GenerateMeetLink();

        Task<MeetingMinutesDto> UploadMinutesAsync(
            string managerId, int meetingId, Stream content, string fileName, string? contentType, long length);

        Task<List<MeetingMinutesDto>> GetMinutesAsync(string userId, int meetingId);
    }
}

using ZEstate.Core.DTOs.Voting;

namespace ZEstate.Core.Interfaces
{
    public interface IVotingService
    {
        Task<List<VoteQuestionResultDto>> GetVoteQuestionsAsync(string userId, int meetingId);
        Task<List<VoteHistoryEntryDto>> GetHistoryAsync(string userId);
        Task<VoteQuestionResultDto> CreateVoteQuestionAsync(string managerId, int meetingId, CreateVoteQuestionDto dto);
        Task CastVoteAsync(string userId, int questionId, CastVoteDto dto);
        Task<VoteQuestionResultDto> GetResultAsync(string userId, int questionId);
    }
}

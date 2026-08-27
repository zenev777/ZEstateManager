// VotingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Voting;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Authorize]
public class VotingController : ControllerBase
{
    private readonly IVotingService _votingService;

    public VotingController(IVotingService votingService)
    {
        _votingService = votingService;
    }

    // GET: Всички въпроси за гласуване на събрание, с текущия резултат за всеки
    [HttpGet("api/meetings/{meetingId:int}/vote-questions")]
    public async Task<IActionResult> GetVoteQuestions(int meetingId) =>
        Ok(await _votingService.GetVoteQuestionsAsync(CurrentUserId, meetingId));

    // GET: Историческа справка - всички гласувания в сградата и дали са били валидни (постигнат кворум)
    [HttpGet("api/voting/history")]
    public async Task<IActionResult> GetHistory() =>
        Ok(await _votingService.GetHistoryAsync(CurrentUserId));

    // POST: Създаване на въпрос за гласуване, обвързан със събрание
    [HttpPost("api/meetings/{meetingId:int}/vote-questions")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateVoteQuestion(int meetingId, [FromBody] CreateVoteQuestionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _votingService.CreateVoteQuestionAsync(CurrentUserId, meetingId, dto));
    }

    // POST: Гласуване по въпрос - веднъж на апартамент, само в рамките на времевия прозорец
    [HttpPost("api/vote-questions/{id:int}/vote")]
    public async Task<IActionResult> CastVote(int id, [FromBody] CastVoteDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _votingService.CastVoteAsync(CurrentUserId, id, dto);
        return Ok(new { message = "Гласът е записан." });
    }

    // GET: Текущ резултат по въпрос
    [HttpGet("api/vote-questions/{id:int}/result")]
    public async Task<IActionResult> GetResult(int id) =>
        Ok(await _votingService.GetResultAsync(CurrentUserId, id));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

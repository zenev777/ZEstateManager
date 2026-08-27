// VotingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Voting;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Authorize]
public class VotingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public VotingController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Всички въпроси за гласуване на събрание, с текущия резултат за всеки
    [HttpGet("api/meetings/{meetingId:int}/vote-questions")]
    public async Task<IActionResult> GetVoteQuestions(int meetingId)
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && m.BuildingId == building.Id);
        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        var questions = await _context.VoteQuestions
            .Where(q => q.MeetingId == meetingId)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .OrderBy(q => q.StartAt)
            .ToListAsync();

        var myApartmentId = await GetMyApartmentIdAsync(building.Id);

        return Ok(questions.Select(q => QuestionResponse(q, totalIdealParts, building.QuorumThresholdPercent, myApartmentId)));
    }

    // GET: Историческа справка - всички гласувания в сградата и дали са били валидни (постигнат кворум)
    [HttpGet("api/voting/history")]
    public async Task<IActionResult> GetHistory()
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        var questions = await _context.VoteQuestions
            .Where(q => q.Meeting.BuildingId == building.Id)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .Include(q => q.Meeting)
            .OrderByDescending(q => q.StartAt)
            .ToListAsync();

        var myApartmentId = await GetMyApartmentIdAsync(building.Id);

        return Ok(questions.Select(q => new
        {
            meetingTitle = q.Meeting.Title,
            question = QuestionResponse(q, totalIdealParts, building.QuorumThresholdPercent, myApartmentId)
        }));
    }

    // POST: Създаване на въпрос за гласуване, обвързан със събрание
    [HttpPost("api/meetings/{meetingId:int}/vote-questions")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateVoteQuestion(int meetingId, [FromBody] CreateVoteQuestionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.EndAt <= dto.StartAt)
            return BadRequest(new { message = "Крайният момент трябва да е след началния." });

        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var meeting = await _context.Meetings
            .Include(m => m.Building)
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.Building.ManagerId == managerId);

        if (meeting == null)
            return NotFound(new { message = "Събранието не е намерено." });

        var question = new VoteQuestion
        {
            MeetingId = meeting.Id,
            Question = dto.Question,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };

        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == meeting.BuildingId)
            .SumAsync(a => a.IdealParts);

        return Ok(QuestionResponse(question, totalIdealParts, meeting.Building.QuorumThresholdPercent, null));
    }

    // POST: Гласуване по въпрос - веднъж на апартамент, само в рамките на времевия прозорец
    [HttpPost("api/vote-questions/{id:int}/vote")]
    public async Task<IActionResult> CastVote(int id, [FromBody] CastVoteDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!Enum.TryParse<VoteValue>(dto.Value, true, out var value))
            return BadRequest(new { message = "Невалидна стойност. Позволени: Yes, No, Abstain." });

        var question = await _context.VoteQuestions.Include(q => q.Meeting).FirstOrDefaultAsync(q => q.Id == id);
        if (question == null)
            return NotFound(new { message = "Въпросът не е намерен." });

        var now = DateTime.UtcNow;
        if (now < question.StartAt || now > question.EndAt)
            return BadRequest(new { message = "Гласуването не е отворено в момента." });

        var apartmentId = await GetMyApartmentIdAsync(question.Meeting.BuildingId);
        if (apartmentId == null)
            return Forbid();

        var alreadyVoted = await _context.Votes.AnyAsync(v => v.VoteQuestionId == id && v.ApartmentId == apartmentId.Value);
        if (alreadyVoted)
            return BadRequest(new { message = "Апартаментът вече е гласувал по този въпрос." });

        _context.Votes.Add(new Vote
        {
            VoteQuestionId = id,
            ApartmentId = apartmentId.Value,
            UserId = CurrentUserId,
            Value = value
        });

        await _context.SaveChangesAsync();

        return Ok(new { message = "Гласът е записан." });
    }

    // GET: Текущ резултат по въпрос
    [HttpGet("api/vote-questions/{id:int}/result")]
    public async Task<IActionResult> GetResult(int id)
    {
        var question = await _context.VoteQuestions
            .Include(q => q.Meeting).ThenInclude(m => m.Building)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question == null)
            return NotFound(new { message = "Въпросът не е намерен." });

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == question.Meeting.BuildingId)
            .SumAsync(a => a.IdealParts);

        var myApartmentId = await GetMyApartmentIdAsync(question.Meeting.BuildingId);

        return Ok(QuestionResponse(question, totalIdealParts, question.Meeting.Building.QuorumThresholdPercent, myApartmentId));
    }

    private static object QuestionResponse(VoteQuestion question, decimal totalIdealParts, decimal quorumThresholdPercent, int? myApartmentId)
    {
        var yesWeight = question.Votes.Where(v => v.Value == VoteValue.Yes).Sum(v => v.Apartment.IdealParts);
        var noWeight = question.Votes.Where(v => v.Value == VoteValue.No).Sum(v => v.Apartment.IdealParts);
        var abstainWeight = question.Votes.Where(v => v.Value == VoteValue.Abstain).Sum(v => v.Apartment.IdealParts);
        var votedWeight = yesWeight + noWeight + abstainWeight;

        var now = DateTime.UtcNow;
        var status = now < question.StartAt ? "Scheduled" : now > question.EndAt ? "Closed" : "Open";

        var turnoutPercent = totalIdealParts > 0 ? Math.Round(votedWeight / totalIdealParts * 100, 1) : 0;
        var quorumMet = turnoutPercent >= quorumThresholdPercent;

        return new
        {
            question.Id,
            question.MeetingId,
            question.Question,
            question.StartAt,
            question.EndAt,
            status,
            hasVoted = myApartmentId.HasValue && question.Votes.Any(v => v.ApartmentId == myApartmentId.Value),
            result = new
            {
                yesWeight,
                noWeight,
                abstainWeight,
                votedWeight,
                totalIdealParts,
                yesPercent = votedWeight > 0 ? Math.Round(yesWeight / votedWeight * 100, 1) : 0,
                noPercent = votedWeight > 0 ? Math.Round(noWeight / votedWeight * 100, 1) : 0,
                abstainPercent = votedWeight > 0 ? Math.Round(abstainWeight / votedWeight * 100, 1) : 0,
                turnoutPercent,
                quorumThresholdPercent,
                quorumMet,
                // Only meaningful once voting has actually closed - regardless of the
                // Yes/No split, a Closed question without quorum is void per ЗУЕС.
                isValid = status == "Closed" ? quorumMet : (bool?)null
            }
        };
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Manager resolves via Building.ManagerId; any other building member (resident,
    // cashier) resolves via their apartment membership.
    private async Task<Building?> GetMyBuildingAsync()
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        return buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;
    }

    // The apartment the current user votes on behalf of - their (first) active
    // apartment membership in the given building. A house manager who also lives
    // in the building would need an ApartmentUser row too; managers without one
    // simply can't cast a vote (they weren't allocated ideal parts to weigh in with).
    private async Task<int?> GetMyApartmentIdAsync(int buildingId) =>
        await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId && au.IsActive && au.Apartment.BuildingId == buildingId)
            .Select(au => (int?)au.ApartmentId)
            .FirstOrDefaultAsync();
}

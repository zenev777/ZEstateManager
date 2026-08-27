using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Voting;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class VotingService : IVotingService
{
    private readonly ApplicationDbContext _context;

    public VotingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VoteQuestionResultDto>> GetVoteQuestionsAsync(string userId, int meetingId)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId && m.BuildingId == building.Id);
        if (meeting == null)
            throw new NotFoundException("Събранието не е намерено.");

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        var questions = await _context.VoteQuestions
            .Where(q => q.MeetingId == meetingId)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .OrderBy(q => q.StartAt)
            .ToListAsync();

        var myApartmentId = await GetMyApartmentIdAsync(userId, building.Id);

        return questions.Select(q => ToDto(q, totalIdealParts, building.QuorumThresholdPercent, myApartmentId)).ToList();
    }

    public async Task<List<VoteHistoryEntryDto>> GetHistoryAsync(string userId)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        var questions = await _context.VoteQuestions
            .Where(q => q.Meeting.BuildingId == building.Id)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .Include(q => q.Meeting)
            .OrderByDescending(q => q.StartAt)
            .ToListAsync();

        var myApartmentId = await GetMyApartmentIdAsync(userId, building.Id);

        return questions.Select(q => new VoteHistoryEntryDto
        {
            MeetingTitle = q.Meeting.Title,
            Question = ToDto(q, totalIdealParts, building.QuorumThresholdPercent, myApartmentId)
        }).ToList();
    }

    public async Task<VoteQuestionResultDto> CreateVoteQuestionAsync(string managerId, int meetingId, CreateVoteQuestionDto dto)
    {
        if (dto.EndAt <= dto.StartAt)
            throw new BadRequestException("Крайният момент трябва да е след началния.");

        var meeting = await _context.Meetings
            .Include(m => m.Building)
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.Building.ManagerId == managerId);

        if (meeting == null)
            throw new NotFoundException("Събранието не е намерено.");

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

        return ToDto(question, totalIdealParts, meeting.Building.QuorumThresholdPercent, null);
    }

    public async Task CastVoteAsync(string userId, int questionId, CastVoteDto dto)
    {
        if (!Enum.TryParse<VoteValue>(dto.Value, true, out var value))
            throw new BadRequestException("Невалидна стойност. Позволени: Yes, No, Abstain.");

        var question = await _context.VoteQuestions.Include(q => q.Meeting).FirstOrDefaultAsync(q => q.Id == questionId);
        if (question == null)
            throw new NotFoundException("Въпросът не е намерен.");

        var now = DateTime.UtcNow;
        if (now < question.StartAt || now > question.EndAt)
            throw new BadRequestException("Гласуването не е отворено в момента.");

        var apartmentId = await GetMyApartmentIdAsync(userId, question.Meeting.BuildingId);
        if (apartmentId == null)
            throw new ForbiddenException();

        var alreadyVoted = await _context.Votes.AnyAsync(v => v.VoteQuestionId == questionId && v.ApartmentId == apartmentId.Value);
        if (alreadyVoted)
            throw new BadRequestException("Апартаментът вече е гласувал по този въпрос.");

        _context.Votes.Add(new Vote
        {
            VoteQuestionId = questionId,
            ApartmentId = apartmentId.Value,
            UserId = userId,
            Value = value
        });

        await _context.SaveChangesAsync();
    }

    public async Task<VoteQuestionResultDto> GetResultAsync(string userId, int questionId)
    {
        var question = await _context.VoteQuestions
            .Include(q => q.Meeting).ThenInclude(m => m.Building)
            .Include(q => q.Votes).ThenInclude(v => v.Apartment)
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            throw new NotFoundException("Въпросът не е намерен.");

        var totalIdealParts = await _context.Apartments
            .Where(a => a.BuildingId == question.Meeting.BuildingId)
            .SumAsync(a => a.IdealParts);

        var myApartmentId = await GetMyApartmentIdAsync(userId, question.Meeting.BuildingId);

        return ToDto(question, totalIdealParts, question.Meeting.Building.QuorumThresholdPercent, myApartmentId);
    }

    private static VoteQuestionResultDto ToDto(VoteQuestion question, decimal totalIdealParts, decimal quorumThresholdPercent, int? myApartmentId)
    {
        var yesWeight = question.Votes.Where(v => v.Value == VoteValue.Yes).Sum(v => v.Apartment.IdealParts);
        var noWeight = question.Votes.Where(v => v.Value == VoteValue.No).Sum(v => v.Apartment.IdealParts);
        var abstainWeight = question.Votes.Where(v => v.Value == VoteValue.Abstain).Sum(v => v.Apartment.IdealParts);
        var votedWeight = yesWeight + noWeight + abstainWeight;

        var now = DateTime.UtcNow;
        var status = now < question.StartAt ? "Scheduled" : now > question.EndAt ? "Closed" : "Open";

        var turnoutPercent = totalIdealParts > 0 ? Math.Round(votedWeight / totalIdealParts * 100, 1) : 0;
        var quorumMet = turnoutPercent >= quorumThresholdPercent;

        return new VoteQuestionResultDto
        {
            Id = question.Id,
            MeetingId = question.MeetingId,
            Question = question.Question,
            StartAt = question.StartAt,
            EndAt = question.EndAt,
            Status = status,
            HasVoted = myApartmentId.HasValue && question.Votes.Any(v => v.ApartmentId == myApartmentId.Value),
            Result = new VoteTallyDto
            {
                YesWeight = yesWeight,
                NoWeight = noWeight,
                AbstainWeight = abstainWeight,
                VotedWeight = votedWeight,
                TotalIdealParts = totalIdealParts,
                YesPercent = votedWeight > 0 ? Math.Round(yesWeight / votedWeight * 100, 1) : 0,
                NoPercent = votedWeight > 0 ? Math.Round(noWeight / votedWeight * 100, 1) : 0,
                AbstainPercent = votedWeight > 0 ? Math.Round(abstainWeight / votedWeight * 100, 1) : 0,
                TurnoutPercent = turnoutPercent,
                QuorumThresholdPercent = quorumThresholdPercent,
                QuorumMet = quorumMet,
                IsValid = status == "Closed" ? quorumMet : (bool?)null
            }
        };
    }

    // Manager resolves via Building.ManagerId; any other building member (resident,
    // cashier) resolves via their apartment membership.
    private async Task<Building> GetMyBuildingOrThrowAsync(string userId)
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == userId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        var building = buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;

        if (building == null)
            throw new NotFoundException("Нямаш сграда.");

        return building;
    }

    // The apartment the current user votes on behalf of - their (first) active
    // apartment membership in the given building. A house manager who also lives
    // in the building would need an ApartmentUser row too; managers without one
    // simply can't cast a vote (they weren't allocated ideal parts to weigh in with).
    private async Task<int?> GetMyApartmentIdAsync(string userId, int buildingId) =>
        await _context.ApartmentUsers
            .Where(au => au.UserId == userId && au.IsActive && au.Apartment.BuildingId == buildingId)
            .Select(au => (int?)au.ApartmentId)
            .FirstOrDefaultAsync();
}

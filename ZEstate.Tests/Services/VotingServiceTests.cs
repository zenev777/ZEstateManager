using ZEstate.Core.DTOs.Voting;
using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class VotingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly VotingService _service;
    private const string ManagerId = "mgr1";
    private const string VoterId = "res1";

    public VotingServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new VotingService(_context);
    }

    public void Dispose() => _context.Dispose();

    private (Building Building, Meeting Meeting, Apartment Apartment) SetUpMeetingWithVoter(decimal quorumThresholdPercent = 50)
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId, QuorumThresholdPercent = quorumThresholdPercent };
        _context.Buildings.Add(building);
        var meeting = new Meeting { Building = building, Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1) };
        _context.Meetings.Add(meeting);
        var apartment = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 100, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = VoterId, IsActive = true });
        _context.SaveChanges();
        return (building, meeting, apartment);
    }

    [Fact]
    public async Task CreateVoteQuestionAsync_EndBeforeStart_ThrowsBadRequest()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var dto = new CreateVoteQuestionDto { Question = "Q?", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(-1) };

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateVoteQuestionAsync(ManagerId, meeting.Id, dto));
    }

    [Fact]
    public async Task CreateVoteQuestionAsync_MeetingNotOwnedByManager_ThrowsNotFound()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var dto = new CreateVoteQuestionDto { Question = "Q?", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateVoteQuestionAsync("someone-else", meeting.Id, dto));
    }

    [Fact]
    public async Task CastVoteAsync_InvalidValue_ThrowsBadRequest()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddMinutes(-5), EndAt = DateTime.UtcNow.AddMinutes(5) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var dto = new CastVoteDto { Value = "Maybe" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CastVoteAsync(VoterId, question.Id, dto));
    }

    [Fact]
    public async Task CastVoteAsync_BeforeWindowOpens_ThrowsBadRequest()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddHours(1), EndAt = DateTime.UtcNow.AddHours(2) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var dto = new CastVoteDto { Value = "Yes" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CastVoteAsync(VoterId, question.Id, dto));
    }

    [Fact]
    public async Task CastVoteAsync_AfterWindowCloses_ThrowsBadRequest()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddHours(-2), EndAt = DateTime.UtcNow.AddHours(-1) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var dto = new CastVoteDto { Value = "Yes" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CastVoteAsync(VoterId, question.Id, dto));
    }

    [Fact]
    public async Task CastVoteAsync_UserWithoutApartmentMembership_ThrowsForbidden()
    {
        var (_, meeting, _) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddMinutes(-5), EndAt = DateTime.UtcNow.AddMinutes(5) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var dto = new CastVoteDto { Value = "Yes" };
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CastVoteAsync("outsider", question.Id, dto));
    }

    [Fact]
    public async Task CastVoteAsync_DoubleVoteFromSameApartment_ThrowsBadRequest()
    {
        var (_, meeting, apartment) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddMinutes(-5), EndAt = DateTime.UtcNow.AddMinutes(5) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();
        _context.Votes.Add(new Vote { VoteQuestionId = question.Id, ApartmentId = apartment.Id, UserId = VoterId, Value = VoteValue.Yes });
        await _context.SaveChangesAsync();

        var dto = new CastVoteDto { Value = "No" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CastVoteAsync(VoterId, question.Id, dto));
    }

    [Fact]
    public async Task GetResultAsync_QuorumMetAndClosed_IsValidTrue()
    {
        var (_, meeting, apartment) = SetUpMeetingWithVoter(quorumThresholdPercent: 50);
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddHours(-2), EndAt = DateTime.UtcNow.AddHours(-1) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();
        // apartment holds 100% of ideal parts and voted -> 100% turnout, well above 50% quorum.
        _context.Votes.Add(new Vote { VoteQuestionId = question.Id, ApartmentId = apartment.Id, UserId = VoterId, Value = VoteValue.Yes });
        await _context.SaveChangesAsync();

        var result = await _service.GetResultAsync(VoterId, question.Id);

        Assert.Equal("Closed", result.Status);
        Assert.True(result.Result.QuorumMet);
        Assert.True(result.Result.IsValid);
        Assert.Equal(100, result.Result.YesPercent);
    }

    [Fact]
    public async Task GetResultAsync_QuorumNotMetAndClosed_IsValidFalse()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId, QuorumThresholdPercent = 50 };
        _context.Buildings.Add(building);
        var meeting = new Meeting { Building = building, Title = "M", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1) };
        _context.Meetings.Add(meeting);
        // Two apartments (50/50 ideal parts) so a single vote is only 50%... make it below by using 3 apartments.
        var apt1 = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 20, Budget = 0 };
        var apt2 = new Apartment { Building = building, Number = "2", Floor = 1, IdealParts = 80, Budget = 0 };
        _context.Apartments.AddRange(apt1, apt2);
        await _context.SaveChangesAsync();

        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddHours(-2), EndAt = DateTime.UtcNow.AddHours(-1) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();
        _context.Votes.Add(new Vote { VoteQuestionId = question.Id, ApartmentId = apt1.Id, UserId = VoterId, Value = VoteValue.Yes });
        await _context.SaveChangesAsync();

        var result = await _service.GetResultAsync("anyone", question.Id);

        Assert.Equal(20, result.Result.TurnoutPercent);
        Assert.False(result.Result.QuorumMet);
        Assert.False(result.Result.IsValid);
    }

    [Fact]
    public async Task GetResultAsync_StillOpen_IsValidIsNull()
    {
        var (_, meeting, apartment) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddMinutes(-5), EndAt = DateTime.UtcNow.AddMinutes(5) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();

        var result = await _service.GetResultAsync(VoterId, question.Id);

        Assert.Equal("Open", result.Status);
        Assert.Null(result.Result.IsValid);
    }

    [Fact]
    public async Task GetResultAsync_QuestionNotFound_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetResultAsync(VoterId, 999));
    }

    [Fact]
    public async Task GetVoteQuestionsAsync_HasVotedReflectsCallersApartment()
    {
        var (_, meeting, apartment) = SetUpMeetingWithVoter();
        var question = new VoteQuestion { MeetingId = meeting.Id, Question = "Q?", StartAt = DateTime.UtcNow.AddMinutes(-5), EndAt = DateTime.UtcNow.AddMinutes(5) };
        _context.VoteQuestions.Add(question);
        await _context.SaveChangesAsync();
        _context.Votes.Add(new Vote { VoteQuestionId = question.Id, ApartmentId = apartment.Id, UserId = VoterId, Value = VoteValue.Yes });
        await _context.SaveChangesAsync();

        var result = await _service.GetVoteQuestionsAsync(VoterId, meeting.Id);

        Assert.True(result.Single().HasVoted);
    }
}

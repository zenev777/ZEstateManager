using Moq;
using ZEstate.Core.DTOs.Buildings;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class BuildingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly BuildingService _service;
    private const string ManagerId = "mgr1";

    public BuildingServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new BuildingService(_context, _notifications.Object);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding(string inviteCode = "CODE001")
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = inviteCode, ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    [Fact]
    public async Task GetMyBuildingAsync_NoManagedBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetMyBuildingAsync(ManagerId));
    }

    [Fact]
    public async Task GetMyBuildingAsync_ReturnsManagedBuilding()
    {
        var building = AddManagedBuilding();

        var result = await _service.GetMyBuildingAsync(ManagerId);

        Assert.Equal(building.Id, result.Id);
    }

    [Fact]
    public async Task RegenerateInviteCodeAsync_ChangesCodeAndLogsEntry()
    {
        var building = AddManagedBuilding("OLD1");

        var result = await _service.RegenerateInviteCodeAsync(ManagerId);

        Assert.NotEqual("OLD1", result.InviteCode);
        Assert.True(result.InviteCodeActive);
        Assert.Equal(0, result.InviteCodeUseCount);
        var log = Assert.Single(_context.InviteCodeLogs);
        Assert.Equal(InviteCodeAction.Regenerated, log.Action);
        Assert.Equal("OLD1", log.OldCode);
    }

    [Fact]
    public async Task UpdateIbanAsync_InvalidFormat_ThrowsBadRequest()
    {
        AddManagedBuilding();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateIbanAsync(ManagerId, "not-an-iban"));
    }

    [Fact]
    public async Task UpdateIbanAsync_Valid_NormalizesAndSaves()
    {
        AddManagedBuilding();

        var result = await _service.UpdateIbanAsync(ManagerId, "bg80 bnbg 9661 1020 3456 78");

        Assert.Equal("BG80BNBG96611020345678", result.Iban);
        Assert.Equal("BG80BNBG96611020345678", _context.Buildings.Single().Iban);
    }

    [Fact]
    public async Task RevokeInviteCodeAsync_DeactivatesCode()
    {
        AddManagedBuilding();

        var result = await _service.RevokeInviteCodeAsync(ManagerId);

        Assert.False(result.InviteCodeActive);
        Assert.Equal(InviteCodeAction.Revoked, _context.InviteCodeLogs.Single().Action);
    }

    [Fact]
    public async Task UpdateInviteCodeLimitsAsync_SetsExpiryAndMaxUses()
    {
        AddManagedBuilding();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        var result = await _service.UpdateInviteCodeLimitsAsync(ManagerId, new InviteCodeLimitsDto { ExpiresAt = expiresAt, MaxUses = 5 });

        Assert.Equal(expiresAt, result.InviteCodeExpiresAt);
        Assert.Equal(5, result.InviteCodeMaxUses);
    }

    [Fact]
    public async Task GetApartmentsAsync_ReturnsSumOfIdealParts()
    {
        var building = AddManagedBuilding();
        _context.Apartments.AddRange(
            new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 30, Budget = 0 },
            new Apartment { BuildingId = building.Id, Number = "2", Floor = 1, IdealParts = 20, Budget = 0 });
        await _context.SaveChangesAsync();

        var result = await _service.GetApartmentsAsync(ManagerId);

        Assert.Equal(2, result.Apartments.Count);
        Assert.Equal(50, result.IdealPartsTotal);
    }

    [Fact]
    public async Task CreateApartmentAsync_DuplicateNumber_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 });
        await _context.SaveChangesAsync();

        var dto = new CreateApartmentDto { Number = "1", Floor = 2, IdealParts = 5 };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateApartmentAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateApartmentAsync_IdealPartsOverflow_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 95, Budget = 0 });
        await _context.SaveChangesAsync();

        var dto = new CreateApartmentDto { Number = "2", Floor = 1, IdealParts = 10 };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateApartmentAsync(ManagerId, dto));
    }

    [Fact]
    public async Task CreateApartmentAsync_ExactlyFillsTo100_Succeeds()
    {
        var building = AddManagedBuilding();
        _context.Apartments.Add(new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 95, Budget = 0 });
        await _context.SaveChangesAsync();

        var dto = new CreateApartmentDto { Number = "2", Floor = 1, IdealParts = 5 };
        var result = await _service.CreateApartmentAsync(ManagerId, dto);

        Assert.Equal("2", result.Number);
    }

    [Fact]
    public async Task UpdateApartmentAsync_NotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        var dto = new UpdateApartmentDto { Number = "9", Floor = 1, IdealParts = 1 };

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateApartmentAsync(ManagerId, 999, dto));
    }

    [Fact]
    public async Task UpdateApartmentAsync_NumberTakenByAnother_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        _context.Apartments.AddRange(
            new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 },
            new Apartment { BuildingId = building.Id, Number = "2", Floor = 1, IdealParts = 10, Budget = 0 });
        await _context.SaveChangesAsync();
        var target = _context.Apartments.Single(a => a.Number == "2");

        var dto = new UpdateApartmentDto { Number = "1", Floor = 1, IdealParts = 10 };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateApartmentAsync(ManagerId, target.Id, dto));
    }

    [Fact]
    public async Task DeleteApartmentAsync_WithResidents_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", Role = ApartmentRole.Owner });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteApartmentAsync(ManagerId, apartment.Id));
    }

    [Fact]
    public async Task DeleteApartmentAsync_NoResidents_Deletes()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        await _service.DeleteApartmentAsync(ManagerId, apartment.Id);

        Assert.Empty(_context.Apartments);
    }

    [Fact]
    public async Task TransferApartmentAsync_InvalidDebtHandling_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.TransferApartmentAsync(ManagerId, apartment.Id, "NotARealValue"));
    }

    [Fact]
    public async Task TransferApartmentAsync_NoActiveOwner_ThrowsBadRequest()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _service.TransferApartmentAsync(ManagerId, apartment.Id, "TransfersToNewOwner"));
    }

    [Fact]
    public async Task TransferApartmentAsync_StaysWithPreviousOwner_KeepsDebtOnPreviousOwner()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.Users.Add(new ApplicationUser { Id = "owner1", Email = "o@b.com", UserName = "o@b.com" });
        await _context.SaveChangesAsync();

        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "owner1", Role = ApartmentRole.Owner, IsActive = true });

        var fee = new Fee { BuildingId = building.Id, Title = "Fee", Amount = 50, Type = FeeType.Fixed, Frequency = FeeFrequency.OneTime, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        _context.Obligations.Add(new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 50, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        var result = await _service.TransferApartmentAsync(ManagerId, apartment.Id, "StaysWithPreviousOwner");

        Assert.Equal(50, result.OutstandingBalance);
        Assert.Equal("owner1", _context.Obligations.Single().PreviousOwnerUserId);
        Assert.False(_context.ApartmentUsers.Single().IsActive);
        _notifications.Verify(n => n.NotifyAsync("owner1", It.IsAny<string>(), It.IsAny<string>(), null, true), Times.Once);
    }

    [Fact]
    public async Task TransferApartmentAsync_TransfersToNewOwner_LeavesObligationsUnassigned()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.Users.Add(new ApplicationUser { Id = "owner1", Email = "o@b.com", UserName = "o@b.com" });
        await _context.SaveChangesAsync();

        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "owner1", Role = ApartmentRole.Owner, IsActive = true });
        var fee = new Fee { BuildingId = building.Id, Title = "Fee", Amount = 50, Type = FeeType.Fixed, Frequency = FeeFrequency.OneTime, DateFrom = DateTime.UtcNow };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        _context.Obligations.Add(new Obligation { ApartmentId = apartment.Id, FeeId = fee.Id, Amount = 50, Status = ObligationStatus.Pending });
        await _context.SaveChangesAsync();

        await _service.TransferApartmentAsync(ManagerId, apartment.Id, "TransfersToNewOwner");

        Assert.Null(_context.Obligations.Single().PreviousOwnerUserId);
    }

    [Fact]
    public async Task ApproveJoinRequestAsync_CreatesApartmentUserAndNotifies()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var joinRequest = new JoinRequest
        {
            BuildingId = building.Id,
            Building = building,
            ApartmentId = apartment.Id,
            Apartment = apartment,
            UserId = "res1",
            RequestedRole = ApartmentRole.Owner,
            Status = JoinRequestStatus.Pending
        };
        _context.JoinRequests.Add(joinRequest);
        await _context.SaveChangesAsync();

        await _service.ApproveJoinRequestAsync(ManagerId, joinRequest.Id);

        Assert.Equal(JoinRequestStatus.Approved, _context.JoinRequests.Single().Status);
        Assert.Single(_context.ApartmentUsers);
        _notifications.Verify(n => n.NotifyAsync("res1", It.IsAny<string>(), It.IsAny<string>(), "/dashboard", true), Times.Once);
    }

    [Fact]
    public async Task ApproveJoinRequestAsync_AlreadyReviewed_ThrowsNotFound()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var joinRequest = new JoinRequest
        {
            BuildingId = building.Id,
            Building = building,
            ApartmentId = apartment.Id,
            Apartment = apartment,
            UserId = "res1",
            Status = JoinRequestStatus.Approved
        };
        _context.JoinRequests.Add(joinRequest);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveJoinRequestAsync(ManagerId, joinRequest.Id));
    }

    [Fact]
    public async Task RejectJoinRequestAsync_WithReason_TrimsAndStoresReason()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var joinRequest = new JoinRequest
        {
            BuildingId = building.Id,
            Building = building,
            ApartmentId = apartment.Id,
            Apartment = apartment,
            UserId = "res1",
            Status = JoinRequestStatus.Pending
        };
        _context.JoinRequests.Add(joinRequest);
        await _context.SaveChangesAsync();

        await _service.RejectJoinRequestAsync(ManagerId, joinRequest.Id, "  no room  ");

        Assert.Equal("no room", _context.JoinRequests.Single().RejectionReason);
    }

    [Fact]
    public async Task RejectJoinRequestAsync_BlankReason_StoresNull()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        var joinRequest = new JoinRequest
        {
            BuildingId = building.Id,
            Building = building,
            ApartmentId = apartment.Id,
            Apartment = apartment,
            UserId = "res1",
            Status = JoinRequestStatus.Pending
        };
        _context.JoinRequests.Add(joinRequest);
        await _context.SaveChangesAsync();

        await _service.RejectJoinRequestAsync(ManagerId, joinRequest.Id, "   ");

        Assert.Null(_context.JoinRequests.Single().RejectionReason);
    }
}

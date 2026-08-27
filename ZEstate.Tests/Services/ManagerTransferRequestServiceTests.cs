using Microsoft.AspNetCore.Identity;
using Moq;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class ManagerTransferRequestServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly ManagerTransferRequestService _service;
    private const string ManagerId = "mgr1";
    private const string NeighborId = "res1";

    public ManagerTransferRequestServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _userManager = TestHelpers.MockUserManager();
        _service = new ManagerTransferRequestService(_context, _userManager.Object, _notifications.Object);
    }

    public void Dispose() => _context.Dispose();

    private (Building Building, Apartment Apartment) AddManagedBuildingWithNeighbor()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        var apartment = new Apartment { Building = building, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = NeighborId, IsActive = true });
        _context.SaveChanges();
        return (building, apartment);
    }

    [Fact]
    public async Task GetStatusAsync_NoPendingTransfer_ReturnsPendingFalse()
    {
        AddManagedBuildingWithNeighbor();

        var result = await _service.GetStatusAsync(ManagerId);

        Assert.False(result.Pending);
    }

    [Fact]
    public async Task InitiateTransferAsync_AlreadyPending_ThrowsBadRequest()
    {
        var (building, _) = AddManagedBuildingWithNeighbor();
        building.PendingManagerTransferToUserId = NeighborId;
        await _context.SaveChangesAsync();

        var currentUser = new ApplicationUser { Id = ManagerId };
        _userManager.Setup(m => m.FindByIdAsync(ManagerId)).ReturnsAsync(currentUser);
        _userManager.Setup(m => m.CheckPasswordAsync(currentUser, "pass")).ReturnsAsync(true);

        var dto = new InitiateManagerTransferDto { ToUserId = NeighborId, Password = "pass" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.InitiateTransferAsync(ManagerId, dto));
    }

    [Fact]
    public async Task InitiateTransferAsync_WrongPassword_ThrowsBadRequest()
    {
        AddManagedBuildingWithNeighbor();
        var currentUser = new ApplicationUser { Id = ManagerId };
        _userManager.Setup(m => m.FindByIdAsync(ManagerId)).ReturnsAsync(currentUser);
        _userManager.Setup(m => m.CheckPasswordAsync(currentUser, "wrong")).ReturnsAsync(false);

        var dto = new InitiateManagerTransferDto { ToUserId = NeighborId, Password = "wrong" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.InitiateTransferAsync(ManagerId, dto));
    }

    [Fact]
    public async Task InitiateTransferAsync_ToSelf_ThrowsBadRequest()
    {
        AddManagedBuildingWithNeighbor();
        var currentUser = new ApplicationUser { Id = ManagerId };
        _userManager.Setup(m => m.FindByIdAsync(ManagerId)).ReturnsAsync(currentUser);
        _userManager.Setup(m => m.CheckPasswordAsync(currentUser, "pass")).ReturnsAsync(true);

        var dto = new InitiateManagerTransferDto { ToUserId = ManagerId, Password = "pass" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.InitiateTransferAsync(ManagerId, dto));
    }

    [Fact]
    public async Task InitiateTransferAsync_TargetNotBuildingMember_ThrowsBadRequest()
    {
        AddManagedBuildingWithNeighbor();
        var currentUser = new ApplicationUser { Id = ManagerId };
        _userManager.Setup(m => m.FindByIdAsync(ManagerId)).ReturnsAsync(currentUser);
        _userManager.Setup(m => m.CheckPasswordAsync(currentUser, "pass")).ReturnsAsync(true);

        var dto = new InitiateManagerTransferDto { ToUserId = "outsider", Password = "pass" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.InitiateTransferAsync(ManagerId, dto));
    }

    [Fact]
    public async Task InitiateTransferAsync_Valid_SetsGracePeriodAndNotifies()
    {
        AddManagedBuildingWithNeighbor();
        var currentUser = new ApplicationUser { Id = ManagerId, FirstName = "M", LastName = "G" };
        var neighbor = new ApplicationUser { Id = NeighborId };
        _userManager.Setup(m => m.FindByIdAsync(ManagerId)).ReturnsAsync(currentUser);
        _userManager.Setup(m => m.CheckPasswordAsync(currentUser, "pass")).ReturnsAsync(true);
        _userManager.Setup(m => m.FindByIdAsync(NeighborId)).ReturnsAsync(neighbor);

        var beforeCall = DateTime.UtcNow;
        var dto = new InitiateManagerTransferDto { ToUserId = NeighborId, Password = "pass" };
        var effectiveAt = await _service.InitiateTransferAsync(ManagerId, dto);

        Assert.True(effectiveAt > beforeCall.AddDays(2).AddMinutes(-1));
        _notifications.Verify(n => n.NotifyAsync(NeighborId, It.IsAny<string>(), It.IsAny<string>(), "/dashboard", true), Times.Once);
    }

    [Fact]
    public async Task CancelTransferAsync_NoPendingTransfer_ThrowsBadRequest()
    {
        AddManagedBuildingWithNeighbor();
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CancelTransferAsync(ManagerId));
    }

    [Fact]
    public async Task CancelTransferAsync_Valid_ClearsPendingFieldsAndNotifies()
    {
        var (building, _) = AddManagedBuildingWithNeighbor();
        building.PendingManagerTransferToUserId = NeighborId;
        building.PendingManagerTransferInitiatedAt = DateTime.UtcNow;
        building.PendingManagerTransferEffectiveAt = DateTime.UtcNow.AddDays(2);
        await _context.SaveChangesAsync();

        await _service.CancelTransferAsync(ManagerId);

        var reloaded = _context.Buildings.Single();
        Assert.Null(reloaded.PendingManagerTransferToUserId);
        Assert.Null(reloaded.PendingManagerTransferEffectiveAt);
        _notifications.Verify(n => n.NotifyAsync(NeighborId, It.IsAny<string>(), It.IsAny<string>(), "/dashboard", true), Times.Once);
    }
}

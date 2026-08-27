using Microsoft.AspNetCore.Identity;
using Moq;
using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class UserRoleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly UserRoleService _service;
    private const string ManagerId = "mgr1";

    public UserRoleServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _userManager = TestHelpers.MockUserManager();
        _service = new UserRoleService(_userManager.Object, _context);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    [Fact]
    public async Task GetBuildingMembersAsync_NoManagedBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetBuildingMembersAsync("stranger"));
    }

    [Fact]
    public async Task GetBuildingMembersAsync_ExcludesManagerAndInactiveMembers()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.Users.AddRange(
            new ApplicationUser { Id = ManagerId, FirstName = "M", LastName = "G", Email = "m@b.com", UserName = "m@b.com" },
            new ApplicationUser { Id = "res1", FirstName = "R", LastName = "1", Email = "r1@b.com", UserName = "r1@b.com" },
            new ApplicationUser { Id = "res2", FirstName = "R", LastName = "2", Email = "r2@b.com", UserName = "r2@b.com" });
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.AddRange(
            new ApartmentUser { ApartmentId = apartment.Id, UserId = ManagerId, Role = ApartmentRole.HouseManager, IsActive = true },
            new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", Role = ApartmentRole.Resident, IsActive = true },
            new ApartmentUser { ApartmentId = apartment.Id, UserId = "res2", Role = ApartmentRole.Resident, IsActive = false });
        await _context.SaveChangesAsync();
        _userManager.Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == "res1"))).ReturnsAsync(new List<string> { RoleNames.Resident });

        var result = await _service.GetBuildingMembersAsync(ManagerId);

        Assert.Single(result);
        Assert.Equal("res1", result[0].UserId);
    }

    [Fact]
    public async Task ChangeRoleAsync_InvalidRole_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.ChangeRoleAsync(ManagerId, actingUserIsAdministrator: false, "target1", "HouseManager"));
    }

    [Fact]
    public async Task ChangeRoleAsync_TargetUserNotFound_ThrowsNotFound()
    {
        _userManager.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeRoleAsync(ManagerId, actingUserIsAdministrator: false, "missing", RoleNames.Cashier));
    }

    [Fact]
    public async Task ChangeRoleAsync_ManagerCannotChangeAnotherManager_ThrowsForbidden()
    {
        var target = new ApplicationUser { Id = "target1" };
        _userManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        _userManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string> { RoleNames.HouseManager });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ChangeRoleAsync(ManagerId, actingUserIsAdministrator: false, "target1", RoleNames.Cashier));
    }

    [Fact]
    public async Task ChangeRoleAsync_ManagerTargetsResidentOutsideOwnBuilding_ThrowsForbidden()
    {
        AddManagedBuilding();
        var target = new ApplicationUser { Id = "target1" };
        _userManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        _userManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string> { RoleNames.Resident });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ChangeRoleAsync(ManagerId, actingUserIsAdministrator: false, "target1", RoleNames.Cashier));
    }

    [Fact]
    public async Task ChangeRoleAsync_ManagerChangesOwnBuildingResident_Succeeds()
    {
        var building = AddManagedBuilding();
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "target1", Role = ApartmentRole.Resident, IsActive = true });
        await _context.SaveChangesAsync();

        var target = new ApplicationUser { Id = "target1" };
        _userManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        _userManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string> { RoleNames.Resident });
        _userManager.Setup(m => m.RemoveFromRolesAsync(target, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(target, RoleNames.Cashier)).ReturnsAsync(IdentityResult.Success);

        await _service.ChangeRoleAsync(ManagerId, actingUserIsAdministrator: false, "target1", RoleNames.Cashier);

        _userManager.Verify(m => m.AddToRoleAsync(target, RoleNames.Cashier), Times.Once);
    }

    [Fact]
    public async Task ChangeRoleAsync_Administrator_BypassesBuildingOwnershipCheck()
    {
        var target = new ApplicationUser { Id = "target1" };
        _userManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        _userManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        _userManager.Setup(m => m.AddToRoleAsync(target, RoleNames.Cashier)).ReturnsAsync(IdentityResult.Success);

        await _service.ChangeRoleAsync("admin1", actingUserIsAdministrator: true, "target1", RoleNames.Cashier);

        _userManager.Verify(m => m.AddToRoleAsync(target, RoleNames.Cashier), Times.Once);
    }
}

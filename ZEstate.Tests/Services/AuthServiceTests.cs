using Microsoft.AspNetCore.Identity;
using Moq;
using ZEstate.Core.DTOs.Auth;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class AuthServiceTests
{
    private static (AuthService Service, Mock<UserManager<ApplicationUser>> UserManager, ApplicationDbContextFixture Db, Mock<IEmailSender> EmailSender, Mock<INotificationService> NotificationService)
        CreateSut()
    {
        var context = TestHelpers.CreateContext();
        var userManager = TestHelpers.MockUserManager();
        var emailSender = new Mock<IEmailSender>();
        var notificationService = new Mock<INotificationService>();
        var config = TestHelpers.BuildConfiguration();

        var service = new AuthService(userManager.Object, config, context, emailSender.Object, notificationService.Object);
        return (service, userManager, new ApplicationDbContextFixture(context), emailSender, notificationService);
    }

    // Thin wrapper so tests can dispose the context without another using-block layer.
    private sealed class ApplicationDbContextFixture : IDisposable
    {
        public ZEstate.Infrastructure.ApplicationDbContext Context { get; }
        public ApplicationDbContextFixture(ZEstate.Infrastructure.ApplicationDbContext context) => Context = context;
        public void Dispose() => Context.Dispose();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenWithRoles()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com", UserName = "a@b.com", FirstName = "A", LastName = "B" };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass123")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Resident" });

        var result = await service.LoginAsync(new LoginDto { Email = "a@b.com", Password = "pass123" });

        Assert.Equal("a@b.com", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Contains("Resident", result.Roles);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorized()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync("missing@b.com")).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginDto { Email = "missing@b.com", Password = "x" }));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com", UserName = "a@b.com" };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginDto { Email = "a@b.com", Password = "wrong" }));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync("dup@b.com")).ReturnsAsync(new ApplicationUser());

        var dto = new RegisterDto { Email = "dup@b.com", Role = "Resident", JoinBuilding = new JoinBuildingDto { InviteCode = "X", ApartmentNumber = "1" } };

        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_HouseManagerWithoutBuilding_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var dto = new RegisterDto { Email = "mgr@b.com", Role = "HouseManager", Building = null };

        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_ResidentWithoutJoinBuilding_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var dto = new RegisterDto { Email = "res@b.com", Role = "Resident", JoinBuilding = null };

        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_ResidentWithInvalidInviteCode_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var dto = new RegisterDto
        {
            Email = "res@b.com",
            Role = "Resident",
            JoinBuilding = new JoinBuildingDto { InviteCode = "NOPE", ApartmentNumber = "1", Status = "Owner" }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_ResidentWithRevokedInviteCode_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        db.Context.Buildings.Add(new Building { Name = "B", Address = "A", InviteCode = "REV1", InviteCodeActive = false });
        await db.Context.SaveChangesAsync();

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var dto = new RegisterDto
        {
            Email = "res@b.com",
            Role = "Resident",
            JoinBuilding = new JoinBuildingDto { InviteCode = "REV1", ApartmentNumber = "1", Status = "Owner" }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_HouseManager_CreatesBuildingAndReturnsInviteCode()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "mgr1")
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "HouseManager")).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { "HouseManager" });

        var dto = new RegisterDto
        {
            FirstName = "Ivan",
            LastName = "Ivanov",
            Email = "mgr@b.com",
            Password = "pass123",
            Role = "HouseManager",
            Building = new CreateBuildingDto { Name = "Building A", Address = "Str 1", LivesInBuilding = true, ApartmentNumber = "5", Floor = 2 }
        };

        var result = await service.RegisterAsync(dto);

        Assert.NotNull(result.BuildingInviteCode);
        Assert.Equal(8, result.BuildingInviteCode!.Length);
        Assert.Single(db.Context.Buildings);
        Assert.Single(db.Context.Apartments);
        Assert.Single(db.Context.ApartmentUsers);
    }

    [Fact]
    public async Task RegisterAsync_Resident_CreatesJoinRequestAndIncrementsUseCount()
    {
        var (service, userManager, db, _, notificationService) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "OK123", InviteCodeUseCount = 0, ManagerId = "mgr1" };
        db.Context.Buildings.Add(building);
        await db.Context.SaveChangesAsync();

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "res1")
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Resident")).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { "Resident" });

        var dto = new RegisterDto
        {
            Email = "res@b.com",
            Password = "pass123",
            Role = "Resident",
            JoinBuilding = new JoinBuildingDto { InviteCode = "OK123", ApartmentNumber = "7", Status = "Owner" }
        };

        await service.RegisterAsync(dto);

        Assert.Single(db.Context.JoinRequests);
        Assert.Equal(1, db.Context.Buildings.Single().InviteCodeUseCount);
        Assert.Equal(ApartmentRole.Owner, db.Context.JoinRequests.Single().RequestedRole);
        notificationService.Verify(n => n.NotifyAsync(
            "mgr1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task GetBuildingByCodeAsync_UnknownCode_ThrowsNotFound()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBuildingByCodeAsync("MISSING"));
    }

    [Fact]
    public async Task GetBuildingByCodeAsync_ExpiredCode_ThrowsBadRequest()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        db.Context.Buildings.Add(new Building
        {
            Name = "B",
            Address = "A",
            InviteCode = "EXP1",
            InviteCodeExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetBuildingByCodeAsync("EXP1"));
    }

    [Fact]
    public async Task GetBuildingByCodeAsync_MaxUsesReached_ThrowsBadRequest()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        db.Context.Buildings.Add(new Building
        {
            Name = "B",
            Address = "A",
            InviteCode = "MAX1",
            InviteCodeMaxUses = 2,
            InviteCodeUseCount = 2
        });
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetBuildingByCodeAsync("MAX1"));
    }

    [Fact]
    public async Task GetBuildingByCodeAsync_ValidCode_ReturnsBuildingInfo()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        db.Context.Buildings.Add(new Building { Name = "B", Address = "A", InviteCode = "GOOD1" });
        await db.Context.SaveChangesAsync();

        var result = await service.GetBuildingByCodeAsync("GOOD1");

        Assert.Equal("B", result.Name);
        Assert.Equal("GOOD1", result.InviteCode);
    }

    [Fact]
    public async Task GetMeAsync_Manager_ReturnsManagerRoleOnly()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var result = await service.GetMeAsync("mgr1", isManager: true);

        Assert.Equal("HouseManager", result.Role);
        Assert.Null(result.MembershipStatus);
    }

    [Fact]
    public async Task GetMeAsync_ResidentWithNoRequests_ReturnsNoneStatus()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var result = await service.GetMeAsync("res1", isManager: false);

        Assert.Equal("Resident", result.Role);
        Assert.Equal("None", result.MembershipStatus);
    }

    [Fact]
    public async Task GetMeAsync_ActiveApartmentMembershipWithoutJoinRequest_ReturnsApproved()
    {
        // Mirrors data seeded directly (e.g. the QA fixture), which creates an
        // active ApartmentUser without ever going through the join-request flow.
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        var apartment = new Apartment { Building = building, Number = "5", Floor = 2, IdealParts = 20, Budget = 0 };
        db.Context.Buildings.Add(building);
        db.Context.Apartments.Add(apartment);
        await db.Context.SaveChangesAsync();
        db.Context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = true });
        await db.Context.SaveChangesAsync();

        var result = await service.GetMeAsync("res1", isManager: false);

        Assert.Equal("Resident", result.Role);
        Assert.Equal("Approved", result.MembershipStatus);
        Assert.Equal("B", result.BuildingName);
        Assert.Equal("5", result.ApartmentNumber);
    }

    [Fact]
    public async Task GetMeAsync_InactiveApartmentMembership_FallsBackToJoinRequestHistory()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        var apartment = new Apartment { Building = building, Number = "5", Floor = 2, IdealParts = 20, Budget = 0 };
        db.Context.Buildings.Add(building);
        db.Context.Apartments.Add(apartment);
        await db.Context.SaveChangesAsync();
        db.Context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = "res1", IsActive = false });
        await db.Context.SaveChangesAsync();

        var result = await service.GetMeAsync("res1", isManager: false);

        Assert.Equal("None", result.MembershipStatus);
    }

    [Fact]
    public async Task GetMeAsync_ResidentRejectedOnce_CanRetryIsTrue()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        var apartment = new Apartment { Building = building, Number = "1", Floor = 0, IdealParts = 0, Budget = 0 };
        db.Context.Buildings.Add(building);
        db.Context.Apartments.Add(apartment);
        db.Context.JoinRequests.Add(new JoinRequest
        {
            UserId = "res1",
            Building = building,
            Apartment = apartment,
            Status = JoinRequestStatus.Rejected,
            RejectionReason = "no space",
            CreatedAt = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var result = await service.GetMeAsync("res1", isManager: false);

        Assert.Equal("Rejected", result.MembershipStatus);
        Assert.True(result.CanRetry);
        Assert.Equal("no space", result.RejectionReason);
    }

    [Fact]
    public async Task GetMeAsync_ResidentRejectedTwice_CanRetryIsFalse()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "C1" };
        var apartment = new Apartment { Building = building, Number = "1", Floor = 0, IdealParts = 0, Budget = 0 };
        db.Context.Buildings.Add(building);
        db.Context.Apartments.Add(apartment);
        db.Context.JoinRequests.AddRange(
            new JoinRequest { UserId = "res1", Building = building, Apartment = apartment, Status = JoinRequestStatus.Rejected, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new JoinRequest { UserId = "res1", Building = building, Apartment = apartment, Status = JoinRequestStatus.Rejected, CreatedAt = DateTime.UtcNow });
        await db.Context.SaveChangesAsync();

        var result = await service.GetMeAsync("res1", isManager: false);

        Assert.False(result.CanRetry);
    }

    [Fact]
    public async Task ResubmitJoinRequestAsync_NoRejectedRequest_ThrowsBadRequest()
    {
        var (service, _, db, _, _) = CreateSut();
        using var _db = db;

        var dto = new JoinBuildingDto { InviteCode = "X", ApartmentNumber = "1", Status = "Owner" };

        await Assert.ThrowsAsync<BadRequestException>(() => service.ResubmitJoinRequestAsync("res1", dto));
    }

    [Fact]
    public async Task ResubmitJoinRequestAsync_AfterOneRejection_CreatesNewPendingRequest()
    {
        var (service, userManager, db, _, notificationService) = CreateSut();
        using var _db = db;

        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = "mgr1" };
        var apartment = new Apartment { Building = building, Number = "1", Floor = 0, IdealParts = 0, Budget = 0 };
        db.Context.Buildings.Add(building);
        db.Context.Apartments.Add(apartment);
        db.Context.JoinRequests.Add(new JoinRequest
        {
            UserId = "res1",
            Building = building,
            Apartment = apartment,
            Status = JoinRequestStatus.Rejected,
            CreatedAt = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        userManager.Setup(m => m.FindByIdAsync("res1")).ReturnsAsync(new ApplicationUser { Id = "res1", FirstName = "R", LastName = "Es" });

        var dto = new JoinBuildingDto { InviteCode = "C1", ApartmentNumber = "2", Status = "Resident" };
        await service.ResubmitJoinRequestAsync("res1", dto);

        Assert.Equal(2, db.Context.JoinRequests.Count());
        Assert.Contains(db.Context.JoinRequests, jr => jr.Status == JoinRequestStatus.Pending && jr.ApartmentId == db.Context.Apartments.Single(a => a.Number == "2").Id);
        notificationService.Verify(n => n.NotifyAsync(
            "mgr1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_DoesNotSendEmail()
    {
        var (service, userManager, db, emailSender, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync("missing@b.com")).ReturnsAsync((ApplicationUser?)null);

        await service.ForgotPasswordAsync("missing@b.com");

        emailSender.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_InactiveUser_DoesNotSendEmail()
    {
        var (service, userManager, db, emailSender, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com", IsActive = false };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);

        await service.ForgotPasswordAsync("a@b.com");

        emailSender.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ActiveUser_SendsResetEmail()
    {
        var (service, userManager, db, emailSender, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com", FirstName = "A", IsActive = true };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");

        await service.ForgotPasswordAsync("a@b.com");

        emailSender.Verify(e => e.SendAsync("a@b.com", It.IsAny<string>(), It.Is<string>(body => body.Contains("reset-token"))), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownEmail_ThrowsBadRequest()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        userManager.Setup(m => m.FindByEmailAsync("missing@b.com")).ReturnsAsync((ApplicationUser?)null);

        var dto = new ResetPasswordDto { Email = "missing@b.com", Token = "t", NewPassword = "newpass1" };
        await Assert.ThrowsAsync<BadRequestException>(() => service.ResetPasswordAsync(dto));
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ThrowsBadRequestWithFriendlyMessage()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com" };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        userManager.Setup(m => m.ResetPasswordAsync(user, "bad-token", "newpass1"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." }));

        var dto = new ResetPasswordDto { Email = "a@b.com", Token = "bad-token", NewPassword = "newpass1" };
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.ResetPasswordAsync(dto));

        Assert.Equal("Невалиден или изтекъл линк за нулиране на паролата.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_Success_CompletesWithoutError()
    {
        var (service, userManager, db, _, _) = CreateSut();
        using var _db = db;

        var user = new ApplicationUser { Id = "u1", Email = "a@b.com" };
        userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        userManager.Setup(m => m.ResetPasswordAsync(user, "good-token", "newpass1")).ReturnsAsync(IdentityResult.Success);

        var dto = new ResetPasswordDto { Email = "a@b.com", Token = "good-token", NewPassword = "newpass1" };
        await service.ResetPasswordAsync(dto);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZEstate.Core.DTOs.Users;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using Xunit;

namespace ZEstate.Tests.Authorization;

public class UsersControllerRoleChangeTests
{
    private static (ApplicationDbContext Db, UserManager<ApplicationUser> Users) BuildIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in RoleNames.All)
            roleManager.CreateAsync(new ApplicationRole { Name = role }).GetAwaiter().GetResult();

        return (db, userManager);
    }

    private static UsersController ControllerAs(ApplicationDbContext db, UserManager<ApplicationUser> users, ApplicationUser caller, string callerRole)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, caller.Id),
            new Claim(ClaimTypes.Role, callerRole),
        }, "TestAuth"));

        return new UsersController(users, db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
        };
    }

    private static async Task<(ApplicationUser Manager, ApplicationUser Resident, Building Building)> SeedManagerWithResidentAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        var manager = new ApplicationUser { UserName = "manager@test.com", Email = "manager@test.com" };
        await users.CreateAsync(manager, "Passw0rd!");
        await users.AddToRoleAsync(manager, RoleNames.HouseManager);

        var resident = new ApplicationUser { UserName = "resident@test.com", Email = "resident@test.com" };
        await users.CreateAsync(resident, "Passw0rd!");
        await users.AddToRoleAsync(resident, RoleNames.Resident);

        var building = new Building { Name = "Blok 1", Address = "Str 1", InviteCode = "ABC12345", ManagerId = manager.Id };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        db.Apartments.Add(apartment);
        await db.SaveChangesAsync();

        db.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = resident.Id, Role = ApartmentRole.Owner });
        await db.SaveChangesAsync();

        return (manager, resident, building);
    }

    [Fact]
    public async Task HouseManager_CanPromoteOwnResidentToCashier()
    {
        var (db, users) = BuildIdentity();
        var (manager, resident, _) = await SeedManagerWithResidentAsync(db, users);
        var controller = ControllerAs(db, users, manager, RoleNames.HouseManager);

        var result = await controller.ChangeRole(resident.Id, new ChangeUserRoleDto { Role = RoleNames.Cashier });

        Assert.IsType<OkObjectResult>(result);
        var roles = await users.GetRolesAsync(resident);
        Assert.Contains(RoleNames.Cashier, roles);
        Assert.DoesNotContain(RoleNames.Resident, roles);
    }

    [Fact]
    public async Task HouseManager_CannotAssignAdministratorRole()
    {
        var (db, users) = BuildIdentity();
        var (manager, resident, _) = await SeedManagerWithResidentAsync(db, users);
        var controller = ControllerAs(db, users, manager, RoleNames.HouseManager);

        var result = await controller.ChangeRole(resident.Id, new ChangeUserRoleDto { Role = RoleNames.Administrator });

        Assert.IsType<BadRequestObjectResult>(result);
        var roles = await users.GetRolesAsync(resident);
        Assert.Contains(RoleNames.Resident, roles);
    }

    [Fact]
    public async Task HouseManager_CannotChangeRoleOfResidentFromAnotherBuilding()
    {
        var (db, users) = BuildIdentity();
        var (_, resident, _) = await SeedManagerWithResidentAsync(db, users);

        var otherManager = new ApplicationUser { UserName = "other@test.com", Email = "other@test.com" };
        await users.CreateAsync(otherManager, "Passw0rd!");
        await users.AddToRoleAsync(otherManager, RoleNames.HouseManager);
        db.Buildings.Add(new Building { Name = "Blok 2", Address = "Str 2", InviteCode = "XYZ98765", ManagerId = otherManager.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, users, otherManager, RoleNames.HouseManager);

        var result = await controller.ChangeRole(resident.Id, new ChangeUserRoleDto { Role = RoleNames.Cashier });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Administrator_CanChangeRoleAcrossBuildings()
    {
        var (db, users) = BuildIdentity();
        var (_, resident, _) = await SeedManagerWithResidentAsync(db, users);

        var admin = new ApplicationUser { UserName = "admin@test.com", Email = "admin@test.com" };
        await users.CreateAsync(admin, "Passw0rd!");
        await users.AddToRoleAsync(admin, RoleNames.Administrator);

        var controller = ControllerAs(db, users, admin, RoleNames.Administrator);

        var result = await controller.ChangeRole(resident.Id, new ChangeUserRoleDto { Role = RoleNames.Cashier });

        Assert.IsType<OkObjectResult>(result);
        var roles = await users.GetRolesAsync(resident);
        Assert.Contains(RoleNames.Cashier, roles);
    }
}

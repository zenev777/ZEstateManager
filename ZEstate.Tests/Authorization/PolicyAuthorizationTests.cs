using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstateApi.Authorization;
using Xunit;

namespace ZEstate.Tests.Authorization;

public class PolicyAuthorizationTests
{
    private readonly IAuthorizationService _authorizationService;

    public PolicyAuthorizationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddZEstatePolicies());

        _authorizationService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "TestAuth"));

    [Theory]
    [InlineData(RoleNames.HouseManager, true)]
    [InlineData(RoleNames.Administrator, true)]
    [InlineData(RoleNames.Resident, false)]
    [InlineData(RoleNames.Cashier, false)]
    public async Task BuildingManagementPolicy_AllowsOnlyHouseManagerAndAdministrator(string role, bool expected)
    {
        var result = await _authorizationService.AuthorizeAsync(UserWithRole(role), PolicyNames.BuildingManagement);
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleNames.Cashier, true)]
    [InlineData(RoleNames.HouseManager, true)]
    [InlineData(RoleNames.Administrator, true)]
    [InlineData(RoleNames.Resident, false)]
    public async Task PaymentsManagementPolicy_AllowsCashierHouseManagerAndAdministrator(string role, bool expected)
    {
        var result = await _authorizationService.AuthorizeAsync(UserWithRole(role), PolicyNames.PaymentsManagement);
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleNames.Administrator, true)]
    [InlineData(RoleNames.HouseManager, false)]
    [InlineData(RoleNames.Cashier, false)]
    [InlineData(RoleNames.Resident, false)]
    public async Task AdministratorPolicy_AllowsOnlyAdministrator(string role, bool expected)
    {
        var result = await _authorizationService.AuthorizeAsync(UserWithRole(role), PolicyNames.Administrator);
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleNames.Resident)]
    [InlineData(RoleNames.Cashier)]
    [InlineData(RoleNames.HouseManager)]
    [InlineData(RoleNames.Administrator)]
    public async Task ResidentAccessPolicy_AllowsAnyKnownRole(string role)
    {
        var result = await _authorizationService.AuthorizeAsync(UserWithRole(role), PolicyNames.ResidentAccess);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task BuildingManagementPolicy_RejectsUnauthenticatedUser()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var result = await _authorizationService.AuthorizeAsync(anonymous, PolicyNames.BuildingManagement);
        Assert.False(result.Succeeded);
    }
}

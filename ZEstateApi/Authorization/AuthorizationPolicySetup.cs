using Microsoft.AspNetCore.Authorization;
using ZEstate.Infrastructure.Data.DataConstants;

namespace ZEstateApi.Authorization;

public static class AuthorizationPolicySetup
{
    // Extracted so the exact same policy definitions can be exercised from tests
    // without spinning up the full host (DB, JWT, etc.) — see Program.cs.
    public static void AddZEstatePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(PolicyNames.Administrator, policy =>
            policy.RequireRole(RoleNames.Administrator));

        options.AddPolicy(PolicyNames.BuildingManagement, policy =>
            policy.RequireRole(RoleNames.HouseManager, RoleNames.Administrator));

        options.AddPolicy(PolicyNames.PaymentsManagement, policy =>
            policy.RequireRole(RoleNames.Cashier, RoleNames.HouseManager, RoleNames.Administrator));

        options.AddPolicy(PolicyNames.ResidentAccess, policy =>
            policy.RequireRole(RoleNames.All));
    }
}

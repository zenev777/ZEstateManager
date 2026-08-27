// QaSeedData.cs
using Microsoft.AspNetCore.Identity;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

// Seeds one ready-to-use building (a house manager + residents) for QA, so they
// have known logins on the test environment instead of registering by hand.
// Only ever runs when "Seed:QaFixture" is explicitly set (see Program.cs) - off
// by default everywhere, including here on "test", so it takes an intentional
// env var flip on the host to populate. Idempotent: checks for the manager
// account first and no-ops if the fixture already exists, so it's safe to leave
// the flag on or trigger the seed more than once.
public static class QaSeedData
{
    public const string Password = "QaTest123";
    private const string ManagerEmail = "qa.manager@zestate.test";

    private record ResidentSeed(
        string Email,
        string FirstName,
        string LastName,
        string ApartmentNumber,
        int Floor,
        decimal IdealParts,
        ApartmentRole ApartmentRole,
        string IdentityRole);

    private static readonly ResidentSeed[] Residents =
    [
        new("qa.resident1@zestate.test", "Иван", "Иванов", "1", 1, 20m, ApartmentRole.Owner, RoleNames.Cashier),
        new("qa.resident2@zestate.test", "Мария", "Петрова", "2", 1, 20m, ApartmentRole.Owner, RoleNames.Resident),
        new("qa.resident3@zestate.test", "Георги", "Георгиев", "3", 2, 15m, ApartmentRole.Owner, RoleNames.Resident),
        new("qa.resident4@zestate.test", "Елена", "Димитрова", "4", 2, 15m, ApartmentRole.Resident, RoleNames.Resident),
        new("qa.resident5@zestate.test", "Стоян", "Николов", "5", 3, 15m, ApartmentRole.Resident, RoleNames.Resident),
        new("qa.resident6@zestate.test", "Виктория", "Стоянова", "6", 3, 15m, ApartmentRole.Owner, RoleNames.Resident),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (await userManager.FindByEmailAsync(ManagerEmail) != null)
            return;

        var manager = new ApplicationUser
        {
            FirstName = "QA",
            LastName = "Домоуправител",
            Email = ManagerEmail,
            UserName = ManagerEmail,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailNotificationsEnabled = true
        };

        await userManager.CreateAsync(manager, Password);
        await userManager.AddToRoleAsync(manager, RoleNames.HouseManager);

        var building = new Building
        {
            Name = "QA Тестова сграда",
            Address = "ул. Тестова 1, София",
            InviteCode = "QATEST01",
            ManagerId = manager.Id,
            CreatedAt = DateTime.UtcNow,
            InviteCodeActive = true,
            QuorumThresholdPercent = 50
        };

        context.Buildings.Add(building);
        await context.SaveChangesAsync();

        foreach (var r in Residents)
        {
            var user = new ApplicationUser
            {
                FirstName = r.FirstName,
                LastName = r.LastName,
                Email = r.Email,
                UserName = r.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                EmailNotificationsEnabled = true
            };

            await userManager.CreateAsync(user, Password);
            await userManager.AddToRoleAsync(user, r.IdentityRole);

            var apartment = new Apartment
            {
                BuildingId = building.Id,
                Number = r.ApartmentNumber,
                Floor = r.Floor,
                IdealParts = r.IdealParts,
                Budget = 0
            };

            context.Apartments.Add(apartment);
            await context.SaveChangesAsync();

            context.ApartmentUsers.Add(new ApartmentUser
            {
                ApartmentId = apartment.Id,
                UserId = user.Id,
                Role = r.ApartmentRole,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }
}

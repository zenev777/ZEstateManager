using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Tests;

public static class TestHelpers
{
    // A fresh in-memory database per call, so tests never see each other's data.
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    // EF Core's InMemory provider can't translate ExecuteUpdate/ExecuteDelete (unlike the
    // real Npgsql provider used in production), so tests that exercise those code paths
    // need a real relational provider - Sqlite in-memory is the lightest one available.
    // Caller must dispose both the context and the returned connection.
    public static ApplicationDbContext CreateSqliteContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    // UserManager's public API is virtual, so Moq can stub it directly without
    // going through IUserStore for the operations our services actually call.
    public static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
    }

    public static IConfiguration BuildConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-characters-long",
            ["Jwt:Issuer"] = "ZEstateApi.Tests",
            ["Jwt:Audience"] = "ZEstateClient.Tests",
            ["Frontend:BaseUrl"] = "http://localhost:4200",
        };

        if (overrides != null)
        {
            foreach (var (key, value) in overrides)
                defaults[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(defaults).Build();
    }
}

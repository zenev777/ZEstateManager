using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

public class ChatServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ChatService _service;
    private const string ManagerId = "mgr1";
    private const string ResidentId = "res1";

    public ChatServiceTests()
    {
        _context = TestHelpers.CreateContext();
        _service = new ChatService(_context);
    }

    public void Dispose() => _context.Dispose();

    private Building AddManagedBuilding()
    {
        var building = new Building { Name = "B", Address = "A", InviteCode = "C1", ManagerId = ManagerId };
        _context.Buildings.Add(building);
        _context.SaveChanges();
        return building;
    }

    private void AddResidentMembership(Building building)
    {
        var apartment = new Apartment { BuildingId = building.Id, Number = "1", Floor = 1, IdealParts = 10, Budget = 0 };
        _context.Apartments.Add(apartment);
        _context.SaveChanges();
        _context.ApartmentUsers.Add(new ApartmentUser { ApartmentId = apartment.Id, UserId = ResidentId, IsActive = true });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetMessagesAsync_NoBuilding_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetMessagesAsync("stranger"));
    }

    [Fact]
    public async Task SendMessageAsync_TrimsMessageAndReturnsBuildingId()
    {
        var building = AddManagedBuilding();
        _context.Users.Add(new ApplicationUser { Id = ManagerId, FirstName = "M", LastName = "Gr", Email = "m@b.com", UserName = "m@b.com" });
        await _context.SaveChangesAsync();

        var result = await _service.SendMessageAsync(ManagerId, "  hello there  ");

        Assert.Equal("hello there", result.Message.Message);
        Assert.Equal(building.Id, result.BuildingId);
        Assert.Single(_context.ChatMessages);
    }

    [Fact]
    public async Task SendMessageAsync_UnknownUser_ThrowsUnauthorized()
    {
        AddManagedBuilding();
        // No ApplicationUser row exists for the manager id, simulating a stale/deleted account.
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.SendMessageAsync(ManagerId, "hi"));
    }

    [Fact]
    public async Task DeleteMessageAsync_NoManagedBuilding_ThrowsNotFoundWithManagerSpecificMessage()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteMessageAsync("stranger", 1));
        Assert.Equal("Нямаш управлявана сграда.", ex.Message);
    }

    [Fact]
    public async Task DeleteMessageAsync_MessageNotFound_ThrowsNotFound()
    {
        AddManagedBuilding();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteMessageAsync(ManagerId, 999));
    }

    [Fact]
    public async Task DeleteMessageAsync_Valid_RemovesMessage()
    {
        var building = AddManagedBuilding();
        _context.Users.Add(new ApplicationUser { Id = ResidentId, FirstName = "R", LastName = "Es", Email = "r@b.com", UserName = "r@b.com" });
        await _context.SaveChangesAsync();
        var message = new ChatMessage { BuildingId = building.Id, UserId = ResidentId, Message = "hi" };
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteMessageAsync(ManagerId, message.Id);

        Assert.Equal(building.Id, result.BuildingId);
        Assert.Empty(_context.ChatMessages);
    }

    [Fact]
    public async Task GetMessagesAsync_ResidentResolvesViaApartmentMembership()
    {
        var building = AddManagedBuilding();
        AddResidentMembership(building);
        _context.Users.Add(new ApplicationUser { Id = ResidentId, FirstName = "R", LastName = "Es", Email = "r@b.com", UserName = "r@b.com" });
        await _context.SaveChangesAsync();
        _context.ChatMessages.Add(new ChatMessage { BuildingId = building.Id, UserId = ResidentId, Message = "hi" });
        await _context.SaveChangesAsync();

        var result = await _service.GetMessagesAsync(ResidentId);

        Assert.Single(result);
    }
}

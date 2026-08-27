// ChatHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Infrastructure;

namespace ZEstateApi.Hubs;

// Pure transport: group membership + pushing events. Persisting/deleting messages
// (and authorizing those actions) happens in ChatController; this hub just relays.
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var buildingId = await GetMyBuildingIdAsync();
        if (buildingId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(buildingId.Value));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var buildingId = await GetMyBuildingIdAsync();
        if (buildingId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(buildingId.Value));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(int buildingId) => $"building-{buildingId}";

    private async Task<int?> GetMyBuildingIdAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return null;

        var managed = await _context.Buildings
            .Where(b => b.ManagerId == userId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        if (managed != null)
            return managed;

        return await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();
    }
}

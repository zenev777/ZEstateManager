// BuildingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

[ApiController]
[Route("api/buildings")]
[Authorize(Roles = "HouseManager")]
public class BuildingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BuildingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Сградата, управлявана от текущия домоуправител
    [HttpGet("my")]
    public async Task<IActionResult> GetMyBuilding()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        return Ok(new
        {
            building.Id,
            building.Name,
            building.Address,
            building.InviteCode
        });
    }

    // GET: Чакащи заявки за присъединяване към сградата
    [HttpGet("my/join-requests")]
    public async Task<IActionResult> GetJoinRequests()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var requests = await _context.JoinRequests
            .Where(jr => jr.BuildingId == building.Id && jr.Status == JoinRequestStatus.Pending)
            .Include(jr => jr.User)
            .Include(jr => jr.Apartment)
            .OrderBy(jr => jr.CreatedAt)
            .Select(jr => new
            {
                jr.Id,
                Name = jr.User.Name,
                Email = jr.User.Email,
                Phone = jr.User.PhoneNumber,
                ApartmentNumber = jr.Apartment.Number,
                jr.RequestedRole,
                jr.Notes,
                jr.CreatedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    // POST: Одобряване на заявка — създава ApartmentUser за живущия
    [HttpPost("join-requests/{id:int}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(int id)
    {
        var joinRequest = await GetPendingJoinRequestAsync(id);
        if (joinRequest == null)
            return NotFound(new { message = "Заявката не е намерена." });

        joinRequest.Status = JoinRequestStatus.Approved;
        joinRequest.ReviewedAt = DateTime.UtcNow;

        _context.ApartmentUsers.Add(new ApartmentUser
        {
            ApartmentId = joinRequest.ApartmentId,
            UserId = joinRequest.UserId,
            Role = joinRequest.RequestedRole,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Заявката е одобрена." });
    }

    // POST: Отхвърляне на заявка
    [HttpPost("join-requests/{id:int}/reject")]
    public async Task<IActionResult> RejectJoinRequest(int id)
    {
        var joinRequest = await GetPendingJoinRequestAsync(id);
        if (joinRequest == null)
            return NotFound(new { message = "Заявката не е намерена." });

        joinRequest.Status = JoinRequestStatus.Rejected;
        joinRequest.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Заявката е отхвърлена." });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<Building?> GetManagedBuildingAsync() =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);

    private async Task<JoinRequest?> GetPendingJoinRequestAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.JoinRequests
            .FirstOrDefaultAsync(jr => jr.Id == id
                                     && jr.BuildingId == building.Id
                                     && jr.Status == JoinRequestStatus.Pending);
    }
}

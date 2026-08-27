// BuildingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Buildings;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/buildings")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class BuildingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public BuildingsController(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    // GET: Сградата, управлявана от текущия домоуправител
    [HttpGet("my")]
    public async Task<IActionResult> GetMyBuilding()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        return Ok(BuildingResponse(building));
    }

    // PUT: Редакция на име/адрес на управляваната сграда
    [HttpPut("my")]
    public async Task<IActionResult> UpdateMyBuilding([FromBody] UpdateBuildingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        building.Name = dto.Name;
        building.Address = dto.Address;

        await _context.SaveChangesAsync();

        return Ok(BuildingResponse(building));
    }

    // POST: Regenerates the invite code - the old one becomes invalid immediately
    [HttpPost("my/invite-code/regenerate")]
    public async Task<IActionResult> RegenerateInviteCode()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var oldCode = building.InviteCode;

        string newCode;
        do
        {
            newCode = GenerateInviteCode();
        } while (await _context.Buildings.AnyAsync(b => b.InviteCode == newCode));

        building.InviteCode = newCode;
        building.InviteCodeActive = true;
        building.InviteCodeUseCount = 0;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = CurrentUserId,
            Action = InviteCodeAction.Regenerated,
            OldCode = oldCode,
            NewCode = newCode
        });

        await _context.SaveChangesAsync();

        return Ok(BuildingResponse(building));
    }

    // POST: Revokes the code without issuing a new one - pauses new registrations
    [HttpPost("my/invite-code/revoke")]
    public async Task<IActionResult> RevokeInviteCode()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        building.InviteCodeActive = false;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = CurrentUserId,
            Action = InviteCodeAction.Revoked,
            OldCode = building.InviteCode
        });

        await _context.SaveChangesAsync();

        return Ok(BuildingResponse(building));
    }

    // PUT: Sets an optional expiration date and/or usage limit for the code
    [HttpPut("my/invite-code/limits")]
    public async Task<IActionResult> UpdateInviteCodeLimits([FromBody] InviteCodeLimitsDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        building.InviteCodeExpiresAt = dto.ExpiresAt;
        building.InviteCodeMaxUses = dto.MaxUses;

        _context.InviteCodeLogs.Add(new InviteCodeLog
        {
            BuildingId = building.Id,
            ChangedByUserId = CurrentUserId,
            Action = InviteCodeAction.LimitsUpdated,
            OldCode = building.InviteCode,
            NewCode = building.InviteCode
        });

        await _context.SaveChangesAsync();

        return Ok(BuildingResponse(building));
    }

    // GET: History of changes made to the invite code
    [HttpGet("my/invite-code/log")]
    public async Task<IActionResult> GetInviteCodeLog()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var log = await _context.InviteCodeLogs
            .Where(l => l.BuildingId == building.Id)
            .Include(l => l.ChangedBy)
            .OrderByDescending(l => l.ChangedAt)
            .Select(l => new
            {
                l.Id,
                l.Action,
                l.OldCode,
                l.NewCode,
                l.ChangedAt,
                ChangedByName = l.ChangedBy.Name
            })
            .ToListAsync();

        return Ok(log);
    }

    // GET: Апартаментите в управляваната сграда + сбор на идеалните части
    [HttpGet("my/apartments")]
    public async Task<IActionResult> GetApartments()
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var apartments = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .OrderBy(a => a.Number)
            .Select(a => new
            {
                a.Id,
                a.Number,
                a.Floor,
                a.IdealParts,
                a.Budget
            })
            .ToListAsync();

        return Ok(new
        {
            apartments,
            idealPartsTotal = apartments.Sum(a => a.IdealParts)
        });
    }

    // POST: Създаване на апартамент в управляваната сграда
    [HttpPost("my/apartments")]
    public async Task<IActionResult> CreateApartment([FromBody] CreateApartmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var building = await GetManagedBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var numberTaken = await _context.Apartments
            .AnyAsync(a => a.BuildingId == building.Id && a.Number == dto.Number);
        if (numberTaken)
            return BadRequest(new { message = "Вече има апартамент с този номер." });

        var currentTotal = await _context.Apartments
            .Where(a => a.BuildingId == building.Id)
            .SumAsync(a => a.IdealParts);

        if (currentTotal + dto.IdealParts > 100)
            return BadRequest(new { message = $"Сборът от идеалните части не може да надвишава 100%. Свободни: {100 - currentTotal}%." });

        var apartment = new Apartment
        {
            BuildingId = building.Id,
            Number = dto.Number,
            Floor = dto.Floor,
            IdealParts = dto.IdealParts,
            Budget = 0
        };

        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            apartment.Id,
            apartment.Number,
            apartment.Floor,
            apartment.IdealParts,
            apartment.Budget
        });
    }

    // PUT: Редакция на апартамент в управляваната сграда
    [HttpPut("my/apartments/{id:int}")]
    public async Task<IActionResult> UpdateApartment(int id, [FromBody] UpdateApartmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var apartment = await GetOwnedApartmentAsync(id);
        if (apartment == null)
            return NotFound(new { message = "Апартаментът не е намерен." });

        var numberTaken = await _context.Apartments
            .AnyAsync(a => a.BuildingId == apartment.BuildingId && a.Number == dto.Number && a.Id != id);
        if (numberTaken)
            return BadRequest(new { message = "Вече има апартамент с този номер." });

        var otherTotal = await _context.Apartments
            .Where(a => a.BuildingId == apartment.BuildingId && a.Id != id)
            .SumAsync(a => a.IdealParts);

        if (otherTotal + dto.IdealParts > 100)
            return BadRequest(new { message = $"Сборът от идеалните части не може да надвишава 100%. Свободни: {100 - otherTotal}%." });

        apartment.Number = dto.Number;
        apartment.Floor = dto.Floor;
        apartment.IdealParts = dto.IdealParts;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            apartment.Id,
            apartment.Number,
            apartment.Floor,
            apartment.IdealParts,
            apartment.Budget
        });
    }

    // DELETE: Изтриване на апартамент в управляваната сграда
    [HttpDelete("my/apartments/{id:int}")]
    public async Task<IActionResult> DeleteApartment(int id)
    {
        var apartment = await GetOwnedApartmentAsync(id);
        if (apartment == null)
            return NotFound(new { message = "Апартаментът не е намерен." });

        var hasResidents = await _context.ApartmentUsers.AnyAsync(au => au.ApartmentId == id);
        if (hasResidents)
            return BadRequest(new { message = "Апартаментът има свързани живущи и не може да бъде изтрит." });

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Апартаментът е изтрит." });
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

        await _notificationService.NotifyAsync(
            joinRequest.UserId,
            "Заявката ти е одобрена",
            $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} е одобрена.",
            "/dashboard");

        return Ok(new { message = "Заявката е одобрена." });
    }

    // POST: Отхвърляне на заявка
    [HttpPost("join-requests/{id:int}/reject")]
    public async Task<IActionResult> RejectJoinRequest(int id, [FromBody] RejectJoinRequestDto? dto)
    {
        var joinRequest = await GetPendingJoinRequestAsync(id);
        if (joinRequest == null)
            return NotFound(new { message = "Заявката не е намерена." });

        joinRequest.Status = JoinRequestStatus.Rejected;
        joinRequest.ReviewedAt = DateTime.UtcNow;
        joinRequest.RejectionReason = string.IsNullOrWhiteSpace(dto?.Reason) ? null : dto.Reason.Trim();

        await _context.SaveChangesAsync();

        var message = joinRequest.RejectionReason != null
            ? $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} беше отхвърлена. Причина: {joinRequest.RejectionReason}"
            : $"Заявката ти за апартамент {joinRequest.Apartment.Number} в {joinRequest.Building.Name} беше отхвърлена.";

        await _notificationService.NotifyAsync(joinRequest.UserId, "Заявката ти беше отхвърлена", message, "/dashboard");

        return Ok(new { message = "Заявката е отхвърлена." });
    }

    private static object BuildingResponse(Building building) => new
    {
        building.Id,
        building.Name,
        building.Address,
        building.InviteCode,
        building.InviteCodeActive,
        building.InviteCodeExpiresAt,
        building.InviteCodeMaxUses,
        building.InviteCodeUseCount
    };

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<Building?> GetManagedBuildingAsync() =>
        _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);

    private async Task<Apartment?> GetOwnedApartmentAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.Apartments
            .FirstOrDefaultAsync(a => a.Id == id && a.BuildingId == building.Id);
    }

    private async Task<JoinRequest?> GetPendingJoinRequestAsync(int id)
    {
        var building = await GetManagedBuildingAsync();
        if (building == null)
            return null;

        return await _context.JoinRequests
            .Include(jr => jr.Building)
            .Include(jr => jr.Apartment)
            .FirstOrDefaultAsync(jr => jr.Id == id
                                     && jr.BuildingId == building.Id
                                     && jr.Status == JoinRequestStatus.Pending);
    }
}

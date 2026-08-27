// ManagerTransferController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/manager-transfer")]
[Authorize]
public class ManagerTransferController : ControllerBase
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(2);

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public ManagerTransferController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // GET: Статус на текущото прехвърляне на права (ако има) - вижда се и от двете страни
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        if (building.PendingManagerTransferToUserId == null)
            return Ok(new { pending = false });

        var toUser = await _userManager.FindByIdAsync(building.PendingManagerTransferToUserId);

        return Ok(new
        {
            pending = true,
            toUserId = building.PendingManagerTransferToUserId,
            toUserName = toUser?.Name,
            initiatedAt = building.PendingManagerTransferInitiatedAt,
            effectiveAt = building.PendingManagerTransferEffectiveAt
        });
    }

    // POST: Стартиране на прехвърляне на права на домоуправител към съсед от сградата.
    // Изисква повторно въвеждане на паролата на текущия домоуправител. Влиза в сила
    // след грейс период от 2 дни, в рамките на който може да бъде отменено.
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> InitiateTransfer([FromBody] InitiateManagerTransferDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUserId = CurrentUserId;
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == currentUserId);
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        if (building.PendingManagerTransferToUserId != null)
            return BadRequest(new { message = "Вече има чакащо прехвърляне на права. Отмени го, преди да стартираш ново." });

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null || !await _userManager.CheckPasswordAsync(currentUser, dto.Password))
            return BadRequest(new { message = "Грешна парола." });

        if (dto.ToUserId == currentUserId)
            return BadRequest(new { message = "Не можеш да прехвърлиш правата на себе си." });

        var belongsToBuilding = await _context.ApartmentUsers
            .AnyAsync(au => au.IsActive && au.UserId == dto.ToUserId && au.Apartment.BuildingId == building.Id);

        if (!belongsToBuilding)
            return BadRequest(new { message = "Избраният съсед не е активен член на тази сграда." });

        var toUser = await _userManager.FindByIdAsync(dto.ToUserId);
        if (toUser == null)
            return NotFound(new { message = "Потребителят не е намерен." });

        var now = DateTime.UtcNow;
        building.PendingManagerTransferToUserId = dto.ToUserId;
        building.PendingManagerTransferInitiatedAt = now;
        building.PendingManagerTransferEffectiveAt = now.Add(GracePeriod);

        await _context.SaveChangesAsync();

        await _notificationService.NotifyAsync(
            dto.ToUserId,
            "Предаване на права на домоуправител",
            $"{currentUser.Name} ти предава правата на домоуправител на {building.Name}. Влиза в сила на {building.PendingManagerTransferEffectiveAt:dd.MM.yyyy HH:mm}, освен ако не бъде отменено.",
            "/dashboard");

        return Ok(new { message = "Прехвърлянето е стартирано.", effectiveAt = building.PendingManagerTransferEffectiveAt });
    }

    // POST: Отмяна на чакащо прехвърляне, докато сме все още в грейс периода
    [HttpPost("cancel")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CancelTransfer()
    {
        var currentUserId = CurrentUserId;
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == currentUserId);
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        if (building.PendingManagerTransferToUserId == null)
            return BadRequest(new { message = "Няма чакащо прехвърляне за отмяна." });

        var toUserId = building.PendingManagerTransferToUserId;

        building.PendingManagerTransferToUserId = null;
        building.PendingManagerTransferInitiatedAt = null;
        building.PendingManagerTransferEffectiveAt = null;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyAsync(
            toUserId,
            "Прехвърлянето на права е отменено",
            $"Прехвърлянето на права на домоуправител на {building.Name} към теб беше отменено.",
            "/dashboard");

        return Ok(new { message = "Прехвърлянето е отменено." });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<Building?> GetMyBuildingAsync()
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        return buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;
    }
}

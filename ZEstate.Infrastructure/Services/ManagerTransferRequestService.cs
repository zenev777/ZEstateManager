using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class ManagerTransferRequestService : IManagerTransferRequestService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(2);

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public ManagerTransferRequestService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    public async Task<ManagerTransferStatusDto> GetStatusAsync(string userId)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);

        if (building.PendingManagerTransferToUserId == null)
            return new ManagerTransferStatusDto { Pending = false };

        var toUser = await _userManager.FindByIdAsync(building.PendingManagerTransferToUserId);

        return new ManagerTransferStatusDto
        {
            Pending = true,
            ToUserId = building.PendingManagerTransferToUserId,
            ToUserName = toUser?.Name,
            InitiatedAt = building.PendingManagerTransferInitiatedAt,
            EffectiveAt = building.PendingManagerTransferEffectiveAt
        };
    }

    // Изисква повторно въвеждане на паролата на текущия домоуправител. Влиза в сила
    // след грейс период от 2 дни, в рамките на който може да бъде отменено.
    public async Task<DateTime> InitiateTransferAsync(string managerId, InitiateManagerTransferDto dto)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        if (building.PendingManagerTransferToUserId != null)
            throw new BadRequestException("Вече има чакащо прехвърляне на права. Отмени го, преди да стартираш ново.");

        var currentUser = await _userManager.FindByIdAsync(managerId);
        if (currentUser == null || !await _userManager.CheckPasswordAsync(currentUser, dto.Password))
            throw new BadRequestException("Грешна парола.");

        if (dto.ToUserId == managerId)
            throw new BadRequestException("Не можеш да прехвърлиш правата на себе си.");

        var belongsToBuilding = await _context.ApartmentUsers
            .AnyAsync(au => au.IsActive && au.UserId == dto.ToUserId && au.Apartment.BuildingId == building.Id);

        if (!belongsToBuilding)
            throw new BadRequestException("Избраният съсед не е активен член на тази сграда.");

        var toUser = await _userManager.FindByIdAsync(dto.ToUserId);
        if (toUser == null)
            throw new NotFoundException("Потребителят не е намерен.");

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

        return building.PendingManagerTransferEffectiveAt.Value;
    }

    public async Task CancelTransferAsync(string managerId)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        if (building.PendingManagerTransferToUserId == null)
            throw new BadRequestException("Няма чакащо прехвърляне за отмяна.");

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
    }

    private async Task<Building> GetMyBuildingOrThrowAsync(string userId)
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == userId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        var building = buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;

        if (building == null)
            throw new NotFoundException("Нямаш сграда.");

        return building;
    }
}

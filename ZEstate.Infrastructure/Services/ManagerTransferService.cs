using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Services;

public class ManagerTransferService : IManagerTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ManagerTransferService> _logger;

    public ManagerTransferService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        ILogger<ManagerTransferService> logger)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<int> ApplyDueTransfersAsync()
    {
        var now = DateTime.UtcNow;

        var dueBuildings = await _context.Buildings
            .Where(b => b.PendingManagerTransferToUserId != null && b.PendingManagerTransferEffectiveAt <= now)
            .ToListAsync();

        foreach (var building in dueBuildings)
        {
            var oldManagerId = building.ManagerId;
            var newManagerId = building.PendingManagerTransferToUserId!;

            var oldManager = oldManagerId != null ? await _userManager.FindByIdAsync(oldManagerId) : null;
            var newManager = await _userManager.FindByIdAsync(newManagerId);

            if (newManager == null)
            {
                // Successor account no longer exists - drop the pending transfer rather than apply it.
                building.PendingManagerTransferToUserId = null;
                building.PendingManagerTransferInitiatedAt = null;
                building.PendingManagerTransferEffectiveAt = null;
                await _context.SaveChangesAsync();
                continue;
            }

            building.ManagerId = newManagerId;

            var newManagerRoles = await _userManager.GetRolesAsync(newManager);
            if (newManagerRoles.Count > 0)
                await _userManager.RemoveFromRolesAsync(newManager, newManagerRoles);
            await _userManager.AddToRoleAsync(newManager, RoleNames.HouseManager);

            if (oldManager != null)
            {
                var oldManagerRoles = await _userManager.GetRolesAsync(oldManager);
                if (oldManagerRoles.Count > 0)
                    await _userManager.RemoveFromRolesAsync(oldManager, oldManagerRoles);
                await _userManager.AddToRoleAsync(oldManager, RoleNames.Resident);
            }

            var apartmentUsers = await _context.ApartmentUsers
                .Where(au => au.Apartment.BuildingId == building.Id
                          && (au.UserId == oldManagerId || au.UserId == newManagerId))
                .ToListAsync();

            foreach (var au in apartmentUsers)
            {
                au.Role = au.UserId == newManagerId ? ApartmentRole.HouseManager : ApartmentRole.Resident;
            }

            building.PendingManagerTransferToUserId = null;
            building.PendingManagerTransferInitiatedAt = null;
            building.PendingManagerTransferEffectiveAt = null;

            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(
                newManagerId,
                "Вече си домоуправител",
                $"Правата на домоуправител на {building.Name} вече са прехвърлени на теб.",
                "/dashboard");

            if (oldManagerId != null)
            {
                await _notificationService.NotifyAsync(
                    oldManagerId,
                    "Прехвърлянето на права приключи",
                    $"Правата на домоуправител на {building.Name} бяха прехвърлени на {newManager.Name}.",
                    "/dashboard");
            }

            _logger.LogInformation(
                "Manager transfer applied for building {BuildingId}: {OldManagerId} -> {NewManagerId}",
                building.Id, oldManagerId, newManagerId);
        }

        return dueBuildings.Count;
    }
}

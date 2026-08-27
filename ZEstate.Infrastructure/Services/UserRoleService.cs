using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.IdentityModels;

namespace ZEstate.Infrastructure.Services;

public class UserRoleService : IUserRoleService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UserRoleService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // GET: Съседите (активните живущи) от управляваната сграда, за панела с роли
    public async Task<List<BuildingMemberDto>> GetBuildingMembersAsync(string managerId)
    {
        var managedBuildingId = await _context.Buildings
            .Where(b => b.ManagerId == managerId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        if (managedBuildingId == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        var members = await _context.ApartmentUsers
            .Where(au => au.IsActive && au.Apartment.BuildingId == managedBuildingId && au.UserId != managerId)
            .Include(au => au.User)
            .Include(au => au.Apartment)
            .OrderBy(au => au.Apartment.Number)
            .ToListAsync();

        var result = new List<BuildingMemberDto>();
        foreach (var member in members)
        {
            var roles = await _userManager.GetRolesAsync(member.User);
            result.Add(new BuildingMemberDto
            {
                UserId = member.UserId,
                Name = member.User.Name,
                Email = member.User.Email,
                ApartmentNumber = member.Apartment.Number,
                Roles = roles
            });
        }

        return result;
    }

    // PUT: Смяна на роля на потребител — само между Собственик/Живущ (Resident) и Касиер (Cashier).
    // Домоуправител/Администратор роли не се раздават оттук (виж RoleNames.Assignable).
    public async Task ChangeRoleAsync(string actingUserId, bool actingUserIsAdministrator, string targetUserId, string role)
    {
        if (!RoleNames.Assignable.Contains(role))
            throw new BadRequestException("Невалидна роля. Позволени стойности: Resident, Cashier.");

        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        if (targetUser == null)
            throw new NotFoundException("Потребителят не е намерен.");

        var currentRoles = await _userManager.GetRolesAsync(targetUser);

        if (!actingUserIsAdministrator)
        {
            // Домоуправителят не може да пипа Домоуправители/Администратори,
            // и може да сменя роля само на живущи от собствената си сграда.
            if (currentRoles.Contains(RoleNames.HouseManager) || currentRoles.Contains(RoleNames.Administrator))
                throw new ForbiddenException();

            var managedBuildingId = await _context.Buildings
                .Where(b => b.ManagerId == actingUserId)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync();

            if (managedBuildingId == null)
                throw new NotFoundException("Нямаш управлявана сграда.");

            var belongsToBuilding = await _context.ApartmentUsers
                .AnyAsync(au => au.UserId == targetUserId && au.Apartment.BuildingId == managedBuildingId);

            if (!belongsToBuilding)
                throw new ForbiddenException();
        }

        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(targetUser, currentRoles);

        await _userManager.AddToRoleAsync(targetUser, role);
    }
}

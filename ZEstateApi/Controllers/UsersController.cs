// UsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.DTOs.Users;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/users")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // PUT: Смяна на роля на потребител — само между Собственик/Живущ (Resident) и Касиер (Cashier).
    // Домоуправител/Администратор роли не се раздават оттук (виж RoleNames.Assignable).
    [HttpPut("{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeUserRoleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!RoleNames.Assignable.Contains(dto.Role))
            return BadRequest(new { message = "Невалидна роля. Позволени стойности: Resident, Cashier." });

        var targetUser = await _userManager.FindByIdAsync(id);
        if (targetUser == null)
            return NotFound(new { message = "Потребителят не е намерен." });

        var currentRoles = await _userManager.GetRolesAsync(targetUser);
        var isAdministrator = User.IsInRole(RoleNames.Administrator);

        if (!isAdministrator)
        {
            // Домоуправителят не може да пипа Домоуправители/Администратори,
            // и може да сменя роля само на живущи от собствената си сграда.
            if (currentRoles.Contains(RoleNames.HouseManager) || currentRoles.Contains(RoleNames.Administrator))
                return Forbid();

            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var managedBuildingId = await _context.Buildings
                .Where(b => b.ManagerId == managerId)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync();

            if (managedBuildingId == null)
                return NotFound(new { message = "Нямаш управлявана сграда." });

            var belongsToBuilding = await _context.ApartmentUsers
                .AnyAsync(au => au.UserId == id && au.Apartment.BuildingId == managedBuildingId);

            if (!belongsToBuilding)
                return Forbid();
        }

        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(targetUser, currentRoles);

        await _userManager.AddToRoleAsync(targetUser, dto.Role);

        return Ok(new { message = "Ролята е сменена.", userId = targetUser.Id, role = dto.Role });
    }
}

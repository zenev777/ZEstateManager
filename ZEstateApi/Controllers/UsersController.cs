// UsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/users")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class UsersController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public UsersController(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    // GET: Съседите (активните живущи) от управляваната сграда, за панела с роли
    [HttpGet("building-members")]
    public async Task<IActionResult> GetBuildingMembers() =>
        Ok(await _userRoleService.GetBuildingMembersAsync(CurrentUserId));

    // PUT: Смяна на роля на потребител — само между Собственик/Живущ (Resident) и Касиер (Cashier).
    [HttpPut("{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeUserRoleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var isAdministrator = User.IsInRole(RoleNames.Administrator);
        await _userRoleService.ChangeRoleAsync(CurrentUserId, isAdministrator, id, dto.Role);

        return Ok(new { message = "Ролята е сменена.", userId = id, role = dto.Role });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

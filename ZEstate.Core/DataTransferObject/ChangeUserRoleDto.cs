// ChangeUserRoleDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Users;

public class ChangeUserRoleDto
{
    // Позволени стойности: "Resident" | "Cashier" — виж RoleNames.Assignable.
    // HouseManager и Administrator не се раздават през този endpoint.
    [Required]
    public string Role { get; set; } = string.Empty;
}

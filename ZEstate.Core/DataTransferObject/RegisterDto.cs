// RegisterDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Auth;

public class RegisterDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Resident"; // "Resident" | "HouseManager"

    // Попълва се само ако Role == HouseManager
    public CreateBuildingDto? Building { get; set; }

    // Попълва се само ако Role == Resident
    public JoinBuildingDto? JoinBuilding { get; set; }
}

public class CreateBuildingDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Range(1, 50)]
    public int FloorsCount { get; set; }

    [Required]
    [Range(1, 500)]
    public int ApartmentsCount { get; set; }
}

public class JoinBuildingDto
{
    [Required]
    [MaxLength(10)]
    public string InviteCode { get; set; } = string.Empty;

    [Required]
    public int ApartmentId { get; set; }
}
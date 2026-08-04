// BuildingManagementDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Buildings;

public class UpdateBuildingDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;
}

public class CreateApartmentDto
{
    [Required]
    [MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    [Required]
    public int Floor { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal IdealParts { get; set; }
}

public class UpdateApartmentDto
{
    [Required]
    [MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    [Required]
    public int Floor { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal IdealParts { get; set; }
}

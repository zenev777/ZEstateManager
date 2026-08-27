// RepairDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Repairs;

public class CreateRepairDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Budget { get; set; }
}

public class UpdateRepairDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Budget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    // "Planned" | "InProgress" | "Completed"
    [Required]
    public string Status { get; set; } = "Planned";
}

public class ManualAllocationEntryDto
{
    [Required]
    public int ApartmentId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}

public class AllocateRepairCostsDto
{
    // If omitted, costs are split proportionally by apartment IdealParts.
    public List<ManualAllocationEntryDto>? ManualAllocations { get; set; }
}

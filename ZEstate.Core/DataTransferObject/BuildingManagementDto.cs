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

public class InviteCodeLimitsDto
{
    // null means no limit
    public DateTime? ExpiresAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxUses { get; set; }
}

public class RejectJoinRequestDto
{
    [MaxLength(300)]
    public string? Reason { get; set; }
}

public class UpdateQuorumThresholdDto
{
    // Minimum % of ideal parts that must vote for quorum. ЗУЕС default is 50.
    [Required]
    [Range(1, 100)]
    public decimal QuorumThresholdPercent { get; set; }
}

public class TransferApartmentDto
{
    // "TransfersToNewOwner" | "StaysWithPreviousOwner"
    [Required]
    public string DebtHandling { get; set; } = "TransfersToNewOwner";
}

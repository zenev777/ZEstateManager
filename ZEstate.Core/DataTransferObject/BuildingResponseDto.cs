// BuildingResponseDto.cs
namespace ZEstate.Core.DTOs.Buildings;

public class BuildingSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public bool InviteCodeActive { get; set; }
    public DateTime? InviteCodeExpiresAt { get; set; }
    public int? InviteCodeMaxUses { get; set; }
    public int InviteCodeUseCount { get; set; }
    public decimal QuorumThresholdPercent { get; set; }
}

public class ApartmentSummaryDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal IdealParts { get; set; }
    public decimal Budget { get; set; }
}

public class ApartmentListDto
{
    public List<ApartmentSummaryDto> Apartments { get; set; } = new();
    public decimal IdealPartsTotal { get; set; }
}

public class InviteCodeLogEntryDto
{
    public int Id { get; set; }

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.InviteCodeAction -
    // kept numeric (not stringified) to match the wire format the frontend already expects.
    public int Action { get; set; }
    public string? OldCode { get; set; }
    public string? NewCode { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
}

public class ApartmentTransferRecordDto
{
    public int Id { get; set; }
    public string? PreviousOwnerName { get; set; }
    public string TransferredByName { get; set; } = string.Empty;

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.DebtHandling.
    public int DebtHandling { get; set; }
    public decimal OutstandingBalanceAtTransfer { get; set; }
    public DateTime TransferredAt { get; set; }
}

public class JoinRequestSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.ApartmentRole.
    public int RequestedRole { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// AuthQueryDto.cs
namespace ZEstate.Core.DTOs.Auth;

public class BuildingByCodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
}

public class MeResponseDto
{
    public string Role { get; set; } = string.Empty;

    // Резидентски полета - null за домоуправител
    public string? MembershipStatus { get; set; }
    public string? BuildingName { get; set; }
    public string? ApartmentNumber { get; set; }
    public bool? CanRetry { get; set; }
    public string? RejectionReason { get; set; }
}

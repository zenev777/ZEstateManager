// BuildingMemberDto.cs
namespace ZEstate.Core.DTOs.Users;

public class BuildingMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
}

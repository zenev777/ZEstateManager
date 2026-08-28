// BuildingRegisterDto.cs
namespace ZEstate.Core.DTOs.Buildings;

// Консолидиран регистър на собствениците/живущите по чл. 7 и чл. 23, ал. 1, т. 6 от ЗУЕС.
public class BuildingRegisterMemberDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    // ApartmentRole: Owner = 0, Resident = 1, HouseManager = 2
    public int Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class BuildingRegisterEntryDto
{
    public string ApartmentNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal IdealParts { get; set; }
    public List<BuildingRegisterMemberDto> Members { get; set; } = new();
}

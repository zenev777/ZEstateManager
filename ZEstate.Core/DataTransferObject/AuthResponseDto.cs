// AuthResponseDto.cs
namespace ZEstate.Core.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();

    // Попълва се само при регистрация на домоуправител
    public string? BuildingInviteCode { get; set; }
}

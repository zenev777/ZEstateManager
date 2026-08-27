using ZEstate.Core.DTOs.Auth;

namespace ZEstate.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<BuildingByCodeDto> GetBuildingByCodeAsync(string code);
        Task<MeResponseDto> GetMeAsync(string userId, bool isManager);
        Task ResubmitJoinRequestAsync(string userId, JoinBuildingDto dto);
        Task ForgotPasswordAsync(string email);
        Task ResetPasswordAsync(ResetPasswordDto dto);
    }
}

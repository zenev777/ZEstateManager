using ZEstate.Core.DTOs.Users;

namespace ZEstate.Core.Interfaces
{
    public interface IUserRoleService
    {
        Task<List<BuildingMemberDto>> GetBuildingMembersAsync(string managerId);
        Task ChangeRoleAsync(string actingUserId, bool actingUserIsAdministrator, string targetUserId, string role);
    }
}

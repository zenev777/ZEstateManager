using ZEstate.Core.DTOs.Fees;

namespace ZEstate.Core.Interfaces
{
    public interface IFeeService
    {
        Task<List<FeeResponseDto>> GetFeesAsync(string managerId);
        Task<FeeResponseDto> CreateFeeAsync(string managerId, CreateFeeDto dto);
        Task<FeeResponseDto> UpdateFeeAsync(string managerId, int feeId, UpdateFeeDto dto);
        Task DeleteFeeAsync(string managerId, int feeId);
        Task<List<ObligationSummaryDto>> GetObligationsAsync(string managerId);
        Task<ObligationsSummaryDto> GetObligationsSummaryAsync(string managerId);

        // Any building member's own obligations (not manager-scoped) - empty list if the
        // caller has no active apartment membership, rather than a NotFound/Forbidden.
        Task<List<ObligationSummaryDto>> GetMyObligationsAsync(string userId);
        Task<ObligationGenerationResult> GenerateObligationsAsync();
        Task<int> MarkOverdueAsync();
    }
}

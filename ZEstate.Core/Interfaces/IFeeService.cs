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
        Task<ObligationGenerationResult> GenerateObligationsAsync();
        Task<int> MarkOverdueAsync();
    }
}

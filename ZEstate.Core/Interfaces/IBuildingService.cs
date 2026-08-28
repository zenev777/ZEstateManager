using ZEstate.Core.DTOs.Buildings;

namespace ZEstate.Core.Interfaces
{
    public record ApartmentTransferResult(decimal OutstandingBalance, string DebtHandling);

    public interface IBuildingService
    {
        Task<BuildingSummaryDto> GetMyBuildingAsync(string managerId);
        Task<BuildingSummaryDto> UpdateMyBuildingAsync(string managerId, UpdateBuildingDto dto);
        Task<BuildingSummaryDto> UpdateIbanAsync(string managerId, string iban);
        Task<BuildingSummaryDto> RegenerateInviteCodeAsync(string managerId);
        Task<BuildingSummaryDto> RevokeInviteCodeAsync(string managerId);
        Task<BuildingSummaryDto> UpdateInviteCodeLimitsAsync(string managerId, InviteCodeLimitsDto dto);
        Task<List<InviteCodeLogEntryDto>> GetInviteCodeLogAsync(string managerId);
        Task<BuildingSummaryDto> UpdateQuorumThresholdAsync(string managerId, decimal quorumThresholdPercent);
        Task<ApartmentListDto> GetApartmentsAsync(string managerId);
        Task<ApartmentSummaryDto> CreateApartmentAsync(string managerId, CreateApartmentDto dto);
        Task<ApartmentSummaryDto> UpdateApartmentAsync(string managerId, int apartmentId, UpdateApartmentDto dto);
        Task DeleteApartmentAsync(string managerId, int apartmentId);
        Task<ApartmentTransferResult> TransferApartmentAsync(string managerId, int apartmentId, string debtHandling);
        Task<List<ApartmentTransferRecordDto>> GetApartmentTransfersAsync(string managerId, int apartmentId);
        Task<List<JoinRequestSummaryDto>> GetJoinRequestsAsync(string managerId);
        Task ApproveJoinRequestAsync(string managerId, int joinRequestId);
        Task RejectJoinRequestAsync(string managerId, int joinRequestId, string? reason);
    }
}

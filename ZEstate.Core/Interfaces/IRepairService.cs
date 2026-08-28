using ZEstate.Core.DTOs.Repairs;

namespace ZEstate.Core.Interfaces
{
    public interface IRepairService
    {
        // Available to any building member (read-only); everything else stays manager-only.
        Task<List<RepairListItemDto>> GetRepairsAsync(string userId);
        Task<RepairResponseDto> CreateRepairAsync(string managerId, CreateRepairDto dto);
        Task<RepairResponseDto> UpdateRepairAsync(string managerId, int repairId, UpdateRepairDto dto);
        Task DeleteRepairAsync(string managerId, int repairId);
        Task<AllocateRepairCostsResultDto> AllocateCostsAsync(string managerId, int repairId, AllocateRepairCostsDto dto);

        Task<UploadedDocumentDto> UploadDocumentAsync(
            string managerId, int repairId, Stream content, string fileName, string? contentType, long length);

        Task<List<RepairDocumentDto>> GetDocumentsAsync(string managerId, int repairId);
    }
}

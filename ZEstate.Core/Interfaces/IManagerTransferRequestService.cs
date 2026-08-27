using ZEstate.Core.DTOs.Users;

namespace ZEstate.Core.Interfaces
{
    // Handles the manager-initiated request/cancel flow (ManagerTransferController).
    // Not to be confused with IManagerTransferService, the background job that applies
    // a transfer once its grace period elapses.
    public interface IManagerTransferRequestService
    {
        Task<ManagerTransferStatusDto> GetStatusAsync(string userId);
        Task<DateTime> InitiateTransferAsync(string managerId, InitiateManagerTransferDto dto);
        Task CancelTransferAsync(string managerId);
    }
}

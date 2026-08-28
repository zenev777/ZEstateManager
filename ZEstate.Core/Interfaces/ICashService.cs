using ZEstate.Core.DTOs.Cash;

namespace ZEstate.Core.Interfaces
{
    public interface ICashService
    {
        // Real balances of the building's two accounts, computed from the ledger.
        Task<CashBalancesDto> GetBalancesAsync(string userId);

        // Records an internal transfer between Cash and Bank as two linked ledger entries.
        Task TransferAsync(string userId, TransferFundsDto dto);

        Task<List<CashLedgerEntryDto>> GetHistoryAsync(string userId);
    }
}

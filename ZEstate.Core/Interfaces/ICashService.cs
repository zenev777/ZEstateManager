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

        // Withdraws money from an account to cover a specific repair's cost - records
        // the withdrawal as a ledger entry linked to the repair, and adds the amount
        // to the repair's ActualCost.
        Task WithdrawForRepairAsync(string userId, WithdrawForRepairDto dto);
    }
}

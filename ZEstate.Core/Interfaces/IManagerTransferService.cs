namespace ZEstate.Core.Interfaces
{
    public interface IManagerTransferService
    {
        // Applies every pending manager transfer whose grace period has elapsed.
        // Returns how many were applied.
        Task<int> ApplyDueTransfersAsync();
    }
}

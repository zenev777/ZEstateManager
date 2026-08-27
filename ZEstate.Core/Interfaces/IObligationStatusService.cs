namespace ZEstate.Core.Interfaces
{
    public interface IObligationStatusService
    {
        // Flips Pending/PartiallyPaid obligations whose DueDate has passed to Overdue
        // and notifies the apartment's residents. Returns how many were marked.
        Task<int> MarkOverdueAsync();
    }
}

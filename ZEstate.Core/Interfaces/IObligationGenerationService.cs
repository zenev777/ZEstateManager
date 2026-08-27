namespace ZEstate.Core.Interfaces
{
    public record ObligationGenerationResult(int Created, int SkippedExisting);

    public interface IObligationGenerationService
    {
        // Generates Obligation rows for every active Fee (Fixed/PerIdealPart) x apartment
        // for the current period. Safe to call repeatedly - already-generated
        // (FeeId, ApartmentId, Period) combinations are skipped.
        Task<ObligationGenerationResult> GenerateForCurrentPeriodAsync();
    }
}

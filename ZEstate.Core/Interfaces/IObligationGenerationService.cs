namespace ZEstate.Core.Interfaces
{
    public record ObligationGenerationResult(int Created, int SkippedExisting);

    public record ObligationPreviewFeeItem(string FeeTitle, int ApartmentCount, decimal TotalAmount);
    public record ObligationGenerationPreview(int ApartmentCount, decimal TotalAmount, List<ObligationPreviewFeeItem> Fees);

    public interface IObligationGenerationService
    {
        // Generates Obligation rows for every active Fee (Fixed/PerIdealPart) x apartment
        // for the current period. Safe to call repeatedly - already-generated
        // (FeeId, ApartmentId, Period) combinations are skipped.
        Task<ObligationGenerationResult> GenerateForCurrentPeriodAsync();

        // Dry-run of the same eligibility/amount logic as GenerateForCurrentPeriodAsync,
        // without persisting anything - lets the caller show what a real run would do
        // (how many apartments, how much) before committing to the irreversible action.
        Task<ObligationGenerationPreview> PreviewForCurrentPeriodAsync();
    }
}

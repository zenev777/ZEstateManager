namespace ZEstate.Core.Interfaces
{
    public record PaymentGatewayResult(bool Succeeded, string? TransactionId, string? Error);

    // Scaffolding for a future online payment method (e.g. Stripe/ePay) — see
    // PaymentMethod.Stripe. No implementation is wired up yet; manual payments
    // (PaymentMethod.Manual) go straight through PaymentsController.
    public interface IPaymentGateway
    {
        Task<PaymentGatewayResult> ChargeAsync(decimal amount, string currency, string description);
    }
}

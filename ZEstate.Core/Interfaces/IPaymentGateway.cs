namespace ZEstate.Core.Interfaces
{
    public record CheckoutSessionResult(string SessionId, string CheckoutUrl);
    public record WebhookCheckoutCompleted(string SessionId, decimal AmountTotal, IReadOnlyDictionary<string, string> Metadata);

    // Abstracts the online payment provider (Stripe) behind an interface so
    // PaymentService stays unit-testable without hitting a real payment API.
    // StripePaymentGateway (ZEstate.Infrastructure) is the only implementation.
    public interface IPaymentGateway
    {
        Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
            decimal amount,
            string currency,
            string description,
            string successUrl,
            string cancelUrl,
            IReadOnlyDictionary<string, string> metadata);

        // Verifies the webhook signature and returns the completed-checkout details,
        // or null if the event isn't a completed checkout (some other Stripe event type).
        WebhookCheckoutCompleted? ParseCheckoutCompletedWebhook(string payload, string signatureHeader);
    }
}

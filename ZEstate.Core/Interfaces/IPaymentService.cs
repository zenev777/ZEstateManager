using ZEstate.Core.DTOs.Payments;

namespace ZEstate.Core.Interfaces
{
    public interface IPaymentService
    {
        Task<RegisterPaymentResultDto> RegisterPaymentAsync(string userId, RegisterPaymentDto dto);
        Task<List<PaymentSummaryDto>> GetPaymentsAsync(string userId, int? apartmentId, DateTime? from, DateTime? to);

        // Any building member paying their own obligation online (Stripe Checkout).
        Task<CheckoutSessionUrlDto> CreateCheckoutSessionAsync(string userId, int obligationId);

        // Called from the (unauthenticated, signature-verified) Stripe webhook endpoint.
        Task HandleStripeWebhookAsync(string payload, string signatureHeader);
    }
}

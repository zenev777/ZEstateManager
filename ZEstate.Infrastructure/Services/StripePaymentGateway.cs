using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

public class StripePaymentGateway : IPaymentGateway
{
    private readonly string _webhookSecret;

    public StripePaymentGateway(IConfiguration configuration)
    {
        // StripeConfiguration.ApiKey is a process-wide static - set it here rather
        // than per-call, since every gateway instance needs the same key anyway.
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        decimal amount,
        string currency,
        string description,
        string successUrl,
        string cancelUrl,
        IReadOnlyDictionary<string, string> metadata)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = (long)Math.Round(amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = description }
                    }
                }
            },
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public WebhookCheckoutCompleted? ParseCheckoutCompletedWebhook(string payload, string signatureHeader)
    {
        Event stripeEvent;
        try
        {
            // The Stripe account's API version can be newer than what this Stripe.net
            // release recognizes (Stripe rolls new API versions faster than the SDK
            // ships). throwOnApiVersionMismatch: false keeps signature verification
            // strict while tolerating that skew instead of hard-failing the webhook.
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new BadRequestException($"Невалиден webhook подпис: {ex.Message}");
        }

        if (stripeEvent.Type != "checkout.session.completed")
            return null;

        if (stripeEvent.Data.Object is not Session session)
            return null;

        var amountTotal = (session.AmountTotal ?? 0) / 100m;
        var metadata = (IReadOnlyDictionary<string, string>?)session.Metadata ?? new Dictionary<string, string>();

        return new WebhookCheckoutCompleted(session.Id, amountTotal, metadata);
    }
}

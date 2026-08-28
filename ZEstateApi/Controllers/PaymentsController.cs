// PaymentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Payments;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // POST: Ръчно регистриране на плащане
    [HttpPost]
    [Authorize(Policy = PolicyNames.PaymentsManagement)]
    public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _paymentService.RegisterPaymentAsync(CurrentUserId, dto));
    }

    // GET: История на плащанията по апартамент, опционално филтрируема по период
    [HttpGet]
    [Authorize(Policy = PolicyNames.PaymentsManagement)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int? apartmentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to) =>
        Ok(await _paymentService.GetPaymentsAsync(CurrentUserId, apartmentId, from, to));

    // POST: Живущият плаща собствено задължение с карта през Stripe Checkout -
    // достъпно за всеки член на сграда, не само домоуправителя/касиера.
    [HttpPost("checkout/{obligationId:int}")]
    public async Task<IActionResult> CreateCheckout(int obligationId) =>
        Ok(await _paymentService.CreateCheckoutSessionAsync(CurrentUserId, obligationId));

    // POST: Stripe webhook - публичен endpoint, автентикиран през подписа на Stripe
    // (Stripe-Signature хедъра), не през JWT, затова е анонимен спрямо ASP.NET auth.
    [HttpPost("stripe-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        await _paymentService.HandleStripeWebhookAsync(json, Request.Headers["Stripe-Signature"]!);

        return Ok();
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

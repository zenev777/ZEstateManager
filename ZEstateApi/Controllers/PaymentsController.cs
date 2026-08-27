// PaymentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Payments;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/payments")]
[Authorize(Policy = PolicyNames.PaymentsManagement)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // POST: Ръчно регистриране на плащане
    [HttpPost]
    public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _paymentService.RegisterPaymentAsync(CurrentUserId, dto));
    }

    // GET: История на плащанията по апартамент, опционално филтрируема по период
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int? apartmentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to) =>
        Ok(await _paymentService.GetPaymentsAsync(CurrentUserId, apartmentId, from, to));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

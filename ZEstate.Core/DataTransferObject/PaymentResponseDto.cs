// PaymentResponseDto.cs
namespace ZEstate.Core.DTOs.Payments;

public class PaymentAllocationDto
{
    public int Id { get; set; }
    public string FeeTitle { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.ObligationStatus.
    public int Status { get; set; }
}

public class RegisterPaymentResultDto
{
    public decimal TotalAmount { get; set; }
    public List<PaymentAllocationDto> Allocations { get; set; } = new();
    public decimal CreditApplied { get; set; }
}

public class CheckoutSessionUrlDto
{
    public string CheckoutUrl { get; set; } = string.Empty;
}

public class PaymentSummaryDto
{
    public int Id { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;
    public string FeeTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.PaymentMethod.
    public int Method { get; set; }
    public string? Note { get; set; }
}

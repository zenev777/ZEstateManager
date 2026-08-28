// PaymentDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Payments;

public class RegisterPaymentDto
{
    [Required]
    public int ApartmentId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaidAt { get; set; }

    // "Manual" | "Stripe"
    [Required]
    public string Method { get; set; } = "Manual";

    // "Cash" | "Bank" - which till the money actually landed in. Only meaningful when
    // Method is "Manual" (Stripe payments always land in "Bank" regardless of this);
    // defaults to "Cash" when omitted.
    public string? Account { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}

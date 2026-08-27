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

    [MaxLength(300)]
    public string? Note { get; set; }
}

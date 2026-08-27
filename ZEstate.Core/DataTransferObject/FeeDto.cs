// FeeDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Fees;

public class CreateFeeDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    // "Fixed" | "PerIdealPart"
    [Required]
    public string Type { get; set; } = "Fixed";

    // "OneTime" | "Monthly"
    [Required]
    public string Frequency { get; set; } = "Monthly";

    [Required]
    public DateTime DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    // "Low" | "Normal" | "High" | "Urgent"
    public string Priority { get; set; } = "Normal";
}

public class UpdateFeeDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string Type { get; set; } = "Fixed";

    [Required]
    public string Frequency { get; set; } = "Monthly";

    [Required]
    public DateTime DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public string Priority { get; set; } = "Normal";
}

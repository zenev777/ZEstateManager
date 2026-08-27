// FeeResponseDto.cs
namespace ZEstate.Core.DTOs.Fees;

public class FeeResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }

    // Underlying int values of the corresponding ZEstate.Infrastructure.Data.Enums types -
    // kept numeric (not stringified) to match the wire format the frontend already expects.
    public int Type { get; set; }
    public int Frequency { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ObligationSummaryDto
{
    public int Id { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;
    public string FeeTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public DateTime? Period { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime DateCreated { get; set; }
}

public class ObligationsStatusBucketDto
{
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class ObligationsSummaryDto
{
    public ObligationsStatusBucketDto Pending { get; set; } = new();
    public ObligationsStatusBucketDto PartiallyPaid { get; set; } = new();
    public ObligationsStatusBucketDto Paid { get; set; } = new();
    public ObligationsStatusBucketDto Overdue { get; set; } = new();
}

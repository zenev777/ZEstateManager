// RepairResponseDto.cs
namespace ZEstate.Core.DTOs.Repairs;

public class RepairResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Budget { get; set; }
    public decimal? ActualCost { get; set; }

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.RepairStatus.
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RepairListItemDto : RepairResponseDto
{
    public bool CostsAllocated { get; set; }
}

public class AllocateRepairCostsResultDto
{
    public int FeeId { get; set; }
    public int ObligationsCreated { get; set; }
    public decimal TotalCost { get; set; }
}

public class RepairDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;

    // Underlying int value of ZEstate.Infrastructure.Data.Enums.DocumentType.
    public int Type { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadedDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

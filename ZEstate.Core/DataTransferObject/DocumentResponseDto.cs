// DocumentResponseDto.cs
namespace ZEstate.Core.DTOs.Documents;

public class DocumentSummaryDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;

    // Underlying int values of the corresponding ZEstate.Infrastructure.Data.Enums types.
    public int Type { get; set; }
    public int Access { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? RepairId { get; set; }
    public int? MeetingId { get; set; }
}

public class UploadedDocumentSummaryDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Access { get; set; }
    public DateTime UploadedAt { get; set; }
}

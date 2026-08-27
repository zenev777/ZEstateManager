using ZEstate.Core.DTOs.Documents;

namespace ZEstate.Core.Interfaces
{
    public record DocumentDownloadResult(Stream Content, string FileName);

    public interface IDocumentService
    {
        Task<List<DocumentSummaryDto>> GetDocumentsAsync(string userId, string? type, DateTime? from, DateTime? to);

        Task<UploadedDocumentSummaryDto> UploadDocumentAsync(
            string managerId, Stream content, string fileName, string? contentType, long length, string type, string access);

        Task DeleteDocumentAsync(string managerId, int documentId);
        Task<DocumentDownloadResult> DownloadAsync(string userId, int documentId);
    }
}

using ZEstate.Core.DTOs.Reports;

namespace ZEstate.Core.Interfaces
{
    public record ReportExportResult(byte[] Content, string FileName);

    public interface IReportService
    {
        Task<FinancialSummaryDto> GetSummaryAsync(string userId, DateTime from, DateTime to);
        Task<List<BalanceHistoryEntryDto>> GetBalanceHistoryAsync(string userId, int months);
        Task<ReportExportResult> ExportAsync(string userId, DateTime from, DateTime to);
    }
}

// ReportsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = PolicyNames.PaymentsManagement)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // GET: Обобщен финансов отчет за период - приходи (плащания), разходи (ремонти) и салдо,
    // с разбивка по апартамент и по категория (тип такса).
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime from, [FromQuery] DateTime to) =>
        Ok(await _reportService.GetSummaryAsync(CurrentUserId, from, to));

    // GET: Салдо по месеци за последните N месеца (за графика)
    [HttpGet("balance-history")]
    public async Task<IActionResult> GetBalanceHistory([FromQuery] int months = 12) =>
        Ok(await _reportService.GetBalanceHistoryAsync(CurrentUserId, months));

    // GET: Износ на отчета за период като CSV (отваря се директно в Excel)
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await _reportService.ExportAsync(CurrentUserId, from, to);
        return File(result.Content, "text/csv", result.FileName);
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

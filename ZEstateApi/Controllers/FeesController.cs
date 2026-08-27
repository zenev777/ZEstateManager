// FeesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Fees;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/fees")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class FeesController : ControllerBase
{
    private readonly IFeeService _feeService;

    public FeesController(IFeeService feeService)
    {
        _feeService = feeService;
    }

    // GET: Всички такси на управляваната сграда
    [HttpGet]
    public async Task<IActionResult> GetFees() =>
        Ok(await _feeService.GetFeesAsync(CurrentUserId));

    // POST: Създаване на такса
    [HttpPost]
    public async Task<IActionResult> CreateFee([FromBody] CreateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _feeService.CreateFeeAsync(CurrentUserId, dto));
    }

    // PUT: Редакция на такса
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFee(int id, [FromBody] UpdateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _feeService.UpdateFeeAsync(CurrentUserId, id, dto));
    }

    // DELETE: Изтриване на такса (само ако все още няма генерирани задължения по нея)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFee(int id)
    {
        await _feeService.DeleteFeeAsync(CurrentUserId, id);
        return Ok(new { message = "Таксата е изтрита." });
    }

    // POST: Ръчно стартиране на генерирането на задължения за текущия период (демо/тест удобство -
    // същата логика, която фоновата задача пуска автоматично всеки ден)
    [HttpPost("generate-obligations")]
    public async Task<IActionResult> GenerateObligations()
    {
        var result = await _feeService.GenerateObligationsAsync();
        return Ok(new { result.Created, result.SkippedExisting });
    }

    // GET: Генерираните задължения на управляваната сграда
    [HttpGet("obligations")]
    public async Task<IActionResult> GetObligations() =>
        Ok(await _feeService.GetObligationsAsync(CurrentUserId));

    // GET: Справка за домоуправителя - брой задължения по статус за цялата сграда
    [HttpGet("obligations/summary")]
    public async Task<IActionResult> GetObligationsSummary() =>
        Ok(await _feeService.GetObligationsSummaryAsync(CurrentUserId));

    // POST: Ръчно стартиране на просрочване (демо/тест удобство - същата логика, която
    // фоновата задача пуска автоматично всеки ден)
    [HttpPost("mark-overdue")]
    public async Task<IActionResult> MarkOverdue()
    {
        var count = await _feeService.MarkOverdueAsync();
        return Ok(new { markedOverdue = count });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

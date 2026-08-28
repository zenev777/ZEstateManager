// FeesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Fees;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/fees")]
[Authorize]
public class FeesController : ControllerBase
{
    private readonly IFeeService _feeService;

    public FeesController(IFeeService feeService)
    {
        _feeService = feeService;
    }

    // GET: Всички такси на управляваната сграда
    [HttpGet]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> GetFees() =>
        Ok(await _feeService.GetFeesAsync(CurrentUserId));

    // POST: Създаване на такса
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateFee([FromBody] CreateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _feeService.CreateFeeAsync(CurrentUserId, dto));
    }

    // PUT: Редакция на такса
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> UpdateFee(int id, [FromBody] UpdateFeeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _feeService.UpdateFeeAsync(CurrentUserId, id, dto));
    }

    // DELETE: Изтриване на такса (само ако все още няма генерирани задължения по нея)
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteFee(int id)
    {
        await _feeService.DeleteFeeAsync(CurrentUserId, id);
        return Ok(new { message = "Таксата е изтрита." });
    }

    // POST: Ръчно стартиране на генерирането на задължения за текущия период (демо/тест удобство -
    // същата логика, която фоновата задача пуска автоматично всеки ден)
    [HttpPost("generate-obligations")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> GenerateObligations()
    {
        var result = await _feeService.GenerateObligationsAsync();
        return Ok(new { result.Created, result.SkippedExisting });
    }

    // GET: Генерираните задължения на управляваната сграда
    [HttpGet("obligations")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> GetObligations() =>
        Ok(await _feeService.GetObligationsAsync(CurrentUserId));

    // GET: Справка за домоуправителя - брой задължения по статус за цялата сграда
    [HttpGet("obligations/summary")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> GetObligationsSummary() =>
        Ok(await _feeService.GetObligationsSummaryAsync(CurrentUserId));

    // GET: Собствените задължения на текущия потребител (за таблото и "Такси и вноски") -
    // достъпно за всеки член на сграда, не само домоуправителя/касиера.
    [HttpGet("my-obligations")]
    public async Task<IActionResult> GetMyObligations() =>
        Ok(await _feeService.GetMyObligationsAsync(CurrentUserId));

    // POST: Ръчно стартиране на просрочване (демо/тест удобство - същата логика, която
    // фоновата задача пуска автоматично всеки ден)
    [HttpPost("mark-overdue")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> MarkOverdue()
    {
        var count = await _feeService.MarkOverdueAsync();
        return Ok(new { markedOverdue = count });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

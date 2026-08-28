// CashController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Cash;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/cash")]
[Authorize(Policy = PolicyNames.PaymentsManagement)]
public class CashController : ControllerBase
{
    private readonly ICashService _cashService;

    public CashController(ICashService cashService)
    {
        _cashService = cashService;
    }

    // GET: Салдо в брой и по банка
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances() =>
        Ok(await _cashService.GetBalancesAsync(CurrentUserId));

    // POST: Вътрешно прехвърляне между двете каси (в брой <-> по банка)
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferFundsDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _cashService.TransferAsync(CurrentUserId, dto);
        return Ok(new { message = "Прехвърлянето е записано." });
    }

    // GET: История на движенията по двете каси
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory() =>
        Ok(await _cashService.GetHistoryAsync(CurrentUserId));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

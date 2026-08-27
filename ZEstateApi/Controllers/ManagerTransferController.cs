// ManagerTransferController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Users;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/manager-transfer")]
[Authorize]
public class ManagerTransferController : ControllerBase
{
    private readonly IManagerTransferRequestService _managerTransferService;

    public ManagerTransferController(IManagerTransferRequestService managerTransferService)
    {
        _managerTransferService = managerTransferService;
    }

    // GET: Статус на текущото прехвърляне на права (ако има) - вижда се и от двете страни
    [HttpGet]
    public async Task<IActionResult> GetStatus() =>
        Ok(await _managerTransferService.GetStatusAsync(CurrentUserId));

    // POST: Стартиране на прехвърляне на права на домоуправител към съсед от сградата.
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> InitiateTransfer([FromBody] InitiateManagerTransferDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var effectiveAt = await _managerTransferService.InitiateTransferAsync(CurrentUserId, dto);

        return Ok(new { message = "Прехвърлянето е стартирано.", effectiveAt });
    }

    // POST: Отмяна на чакащо прехвърляне, докато сме все още в грейс периода
    [HttpPost("cancel")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CancelTransfer()
    {
        await _managerTransferService.CancelTransferAsync(CurrentUserId);
        return Ok(new { message = "Прехвърлянето е отменено." });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

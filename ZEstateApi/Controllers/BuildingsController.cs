// BuildingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Buildings;
using ZEstate.Core.Interfaces;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/buildings")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class BuildingsController : ControllerBase
{
    private readonly IBuildingService _buildingService;

    public BuildingsController(IBuildingService buildingService)
    {
        _buildingService = buildingService;
    }

    // GET: Сградата, управлявана от текущия домоуправител
    [HttpGet("my")]
    public async Task<IActionResult> GetMyBuilding() =>
        Ok(await _buildingService.GetMyBuildingAsync(CurrentUserId));

    // PUT: Редакция на име/адрес на управляваната сграда
    [HttpPut("my")]
    public async Task<IActionResult> UpdateMyBuilding([FromBody] UpdateBuildingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _buildingService.UpdateMyBuildingAsync(CurrentUserId, dto));
    }

    // PUT: Задаване/смяна на IBAN на сградата - изисква се преди резидентите да
    // могат да плащат онлайн (Stripe checkout).
    [HttpPut("my/iban")]
    public async Task<IActionResult> UpdateIban([FromBody] UpdateBuildingIbanDto dto) =>
        Ok(await _buildingService.UpdateIbanAsync(CurrentUserId, dto.Iban));

    // POST: Regenerates the invite code - the old one becomes invalid immediately
    [HttpPost("my/invite-code/regenerate")]
    public async Task<IActionResult> RegenerateInviteCode() =>
        Ok(await _buildingService.RegenerateInviteCodeAsync(CurrentUserId));

    // POST: Revokes the code without issuing a new one - pauses new registrations
    [HttpPost("my/invite-code/revoke")]
    public async Task<IActionResult> RevokeInviteCode() =>
        Ok(await _buildingService.RevokeInviteCodeAsync(CurrentUserId));

    // PUT: Sets an optional expiration date and/or usage limit for the code
    [HttpPut("my/invite-code/limits")]
    public async Task<IActionResult> UpdateInviteCodeLimits([FromBody] InviteCodeLimitsDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _buildingService.UpdateInviteCodeLimitsAsync(CurrentUserId, dto));
    }

    // GET: History of changes made to the invite code
    [HttpGet("my/invite-code/log")]
    public async Task<IActionResult> GetInviteCodeLog() =>
        Ok(await _buildingService.GetInviteCodeLogAsync(CurrentUserId));

    // PUT: Смяна на прага за кворум (% идеални части) при гласувания. По подразбиране 50 (ЗУЕС).
    [HttpPut("my/quorum-threshold")]
    public async Task<IActionResult> UpdateQuorumThreshold([FromBody] UpdateQuorumThresholdDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _buildingService.UpdateQuorumThresholdAsync(CurrentUserId, dto.QuorumThresholdPercent));
    }

    // GET: Апартаментите в управляваната сграда + сбор на идеалните части
    [HttpGet("my/apartments")]
    public async Task<IActionResult> GetApartments() =>
        Ok(await _buildingService.GetApartmentsAsync(CurrentUserId));

    // POST: Създаване на апартамент в управляваната сграда
    [HttpPost("my/apartments")]
    public async Task<IActionResult> CreateApartment([FromBody] CreateApartmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _buildingService.CreateApartmentAsync(CurrentUserId, dto));
    }

    // PUT: Редакция на апартамент в управляваната сграда
    [HttpPut("my/apartments/{id:int}")]
    public async Task<IActionResult> UpdateApartment(int id, [FromBody] UpdateApartmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _buildingService.UpdateApartmentAsync(CurrentUserId, id, dto));
    }

    // DELETE: Изтриване на апартамент в управляваната сграда
    [HttpDelete("my/apartments/{id:int}")]
    public async Task<IActionResult> DeleteApartment(int id)
    {
        await _buildingService.DeleteApartmentAsync(CurrentUserId, id);
        return Ok(new { message = "Апартаментът е изтрит." });
    }

    // POST: Маркиране на апартамент като прехвърлен - старият собственик губи достъп,
    // а новият се регистрира отделно през кода за покана на сградата (същия номер
    // апартамент), минавайки през обичайния поток за присъединяване/одобрение.
    [HttpPost("my/apartments/{id:int}/transfer")]
    public async Task<IActionResult> TransferApartment(int id, [FromBody] TransferApartmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _buildingService.TransferApartmentAsync(CurrentUserId, id, dto.DebtHandling);

        return Ok(new
        {
            message = "Апартаментът е маркиран като прехвърлен.",
            outstandingBalance = result.OutstandingBalance,
            debtHandling = result.DebtHandling
        });
    }

    // GET: История на прехвърлянията на апартамент - за одиторски цели
    [HttpGet("my/apartments/{id:int}/transfers")]
    public async Task<IActionResult> GetApartmentTransfers(int id) =>
        Ok(await _buildingService.GetApartmentTransfersAsync(CurrentUserId, id));

    // GET: Консолидиран регистър на собствениците/живущите (чл. 7, чл. 23 ЗУЕС)
    [HttpGet("my/register")]
    public async Task<IActionResult> GetRegister() =>
        Ok(await _buildingService.GetRegisterAsync(CurrentUserId));

    // GET: Износ на регистъра като CSV (отваря се директно в Excel)
    [HttpGet("my/register/export")]
    public async Task<IActionResult> ExportRegister()
    {
        var result = await _buildingService.ExportRegisterAsync(CurrentUserId);
        return File(result.Content, "text/csv", result.FileName);
    }

    // GET: Чакащи заявки за присъединяване към сградата
    [HttpGet("my/join-requests")]
    public async Task<IActionResult> GetJoinRequests() =>
        Ok(await _buildingService.GetJoinRequestsAsync(CurrentUserId));

    // POST: Одобряване на заявка — създава ApartmentUser за живущия
    [HttpPost("join-requests/{id:int}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(int id)
    {
        await _buildingService.ApproveJoinRequestAsync(CurrentUserId, id);
        return Ok(new { message = "Заявката е одобрена." });
    }

    // POST: Отхвърляне на заявка
    [HttpPost("join-requests/{id:int}/reject")]
    public async Task<IActionResult> RejectJoinRequest(int id, [FromBody] RejectJoinRequestDto? dto)
    {
        await _buildingService.RejectJoinRequestAsync(CurrentUserId, id, dto?.Reason);
        return Ok(new { message = "Заявката е отхвърлена." });
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

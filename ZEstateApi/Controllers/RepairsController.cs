// RepairsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Repairs;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Services;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/repairs")]
[Authorize]
public class RepairsController : ControllerBase
{
    private readonly IRepairService _repairService;

    public RepairsController(IRepairService repairService)
    {
        _repairService = repairService;
    }

    // GET: Ремонтите на сградата - вижда се от всеки неин член, не само домоуправителя
    [HttpGet]
    public async Task<IActionResult> GetRepairs() =>
        Ok(await _repairService.GetRepairsAsync(CurrentUserId));

    // POST: Създаване на ремонт
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> CreateRepair([FromBody] CreateRepairDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _repairService.CreateRepairAsync(CurrentUserId, dto));
    }

    // PUT: Редакция на ремонт (вкл. статус и реален разход)
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> UpdateRepair(int id, [FromBody] UpdateRepairDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _repairService.UpdateRepairAsync(CurrentUserId, id, dto));
    }

    // DELETE: Изтриване на ремонт (само ако разходите все още не са разпределени)
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteRepair(int id)
    {
        await _repairService.DeleteRepairAsync(CurrentUserId, id);
        return Ok(new { message = "Ремонтът е изтрит." });
    }

    // POST: Разпределяне на разходите по апартаментите - пропорционално на идеалните части,
    // или по ръчно зададено разпределение - и създаване на съответните Obligations.
    // Позволено само веднъж на ремонт и само след като е одобрен/в процес/завършен (не Planned).
    [HttpPost("{id:int}/allocate-costs")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> AllocateCosts(int id, [FromBody] AllocateRepairCostsDto dto) =>
        Ok(await _repairService.AllocateCostsAsync(CurrentUserId, id, dto));

    // POST: Прикачване на фактура/документ към ремонт
    [HttpPost("{id:int}/documents")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    [RequestSizeLimit(DocumentUploadValidation.MaxBytes)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var result = await _repairService.UploadDocumentAsync(CurrentUserId, id, stream, file.FileName, file.ContentType, file.Length);

        return Ok(result);
    }

    // GET: Прикачените документи на ремонт (фактури) - остава само за домоуправителя
    [HttpGet("{id:int}/documents")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> GetDocuments(int id) =>
        Ok(await _repairService.GetDocumentsAsync(CurrentUserId, id));

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

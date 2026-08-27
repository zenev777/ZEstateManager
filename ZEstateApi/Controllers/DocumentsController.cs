// DocumentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Services;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    // GET: Списък с документи на сградата - филтрируем по категория и период на качване.
    // Домоуправителят вижда всичко; останалите членове - само тези с достъп "All".
    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to) =>
        Ok(await _documentService.GetDocumentsAsync(CurrentUserId, type, from, to));

    // POST: Качване на общ документ на сградата (протокол/договор/фактура/друго),
    // без да е задължително обвързан с конкретен ремонт или събрание.
    [HttpPost]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    [RequestSizeLimit(DocumentUploadValidation.MaxBytes)]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromForm] string type,
        [FromForm] string access)
    {
        await using var stream = file.OpenReadStream();
        var result = await _documentService.UploadDocumentAsync(
            CurrentUserId, stream, file.FileName, file.ContentType, file.Length, type, access);

        return Ok(result);
    }

    // DELETE: Изтриване на документ - само домоуправителят/администраторът
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        await _documentService.DeleteDocumentAsync(CurrentUserId, id);
        return Ok(new { message = "Документът е изтрит." });
    }

    // GET: Сваляне на прикачен документ - домоуправителят винаги, а обикновен член
    // на сградата само ако документът е с достъп "All" (напр. протокол от събрание).
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var result = await _documentService.DownloadAsync(CurrentUserId, id);
        return File(result.Content, "application/octet-stream", result.FileName);
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

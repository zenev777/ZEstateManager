// DocumentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public DocumentsController(ApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // GET: Списък с документи на сградата - филтрируем по категория и период на качване.
    // Домоуправителят вижда всичко; останалите членове - само тези с достъп "All".
    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var building = await GetMyBuildingAsync();
        if (building == null)
            return NotFound(new { message = "Нямаш сграда." });

        var isManager = building.ManagerId == CurrentUserId;

        var query = _context.Documents.Where(d => d.BuildingId == building.Id);

        if (!isManager)
            query = query.Where(d => d.Access == DocumentAccess.All);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<DocumentType>(type, true, out var parsedType))
                return BadRequest(new { message = "Невалидна категория." });

            query = query.Where(d => d.Type == parsedType);
        }

        if (from.HasValue)
            query = query.Where(d => d.UploadedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(d => d.UploadedAt <= to.Value);

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.Type,
                d.Access,
                d.UploadedAt,
                d.RepairId,
                d.MeetingId
            })
            .ToListAsync();

        return Ok(documents);
    }

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
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var validationError = DocumentUploadValidation.Validate(file);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        if (!Enum.TryParse<DocumentType>(type, true, out var documentType))
            return BadRequest(new { message = "Невалидна категория. Позволени: Protocol, Contract, Invoice, Other." });

        if (!Enum.TryParse<DocumentAccess>(access, true, out var documentAccess))
            return BadRequest(new { message = "Невалиден достъп. Позволени: All, ManagerOnly." });

        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveAsync(stream, file.FileName);

        var document = new Document
        {
            BuildingId = building.Id,
            FilePath = storagePath,
            FileName = file.FileName,
            Type = documentType,
            Access = documentAccess
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Ok(new { document.Id, document.FileName, document.Type, document.Access, document.UploadedAt });
    }

    // DELETE: Изтриване на документ - само домоуправителят/администраторът
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.BuildingManagement)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (building == null)
            return NotFound(new { message = "Нямаш управлявана сграда." });

        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.BuildingId == building.Id);
        if (document == null)
            return NotFound(new { message = "Документът не е намерен." });

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        _fileStorage.Delete(document.FilePath);

        return Ok(new { message = "Документът е изтрит." });
    }

    // GET: Сваляне на прикачен документ - домоуправителят винаги, а обикновен член
    // на сградата само ако документът е с достъп "All" (напр. протокол от събрание).
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var document = await _context.Documents.Include(d => d.Building).FirstOrDefaultAsync(d => d.Id == id);
        if (document == null)
            return NotFound(new { message = "Документът не е намерен." });

        var isManager = document.Building.ManagerId == CurrentUserId;
        if (!isManager)
        {
            var isBuildingMember = await _context.ApartmentUsers
                .AnyAsync(au => au.UserId == CurrentUserId && au.Apartment.BuildingId == document.BuildingId);

            if (!isBuildingMember || document.Access == DocumentAccess.ManagerOnly)
                return NotFound(new { message = "Документът не е намерен." });
        }

        var stream = await _fileStorage.OpenReadAsync(document.FilePath);
        if (stream == null)
            return NotFound(new { message = "Файлът липсва в хранилището." });

        return File(stream, "application/octet-stream", document.FileName);
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<Building?> GetMyBuildingAsync()
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == CurrentUserId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == CurrentUserId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        return buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;
    }
}

// DocumentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstateApi.Authorization;

[ApiController]
[Route("api/documents")]
[Authorize(Policy = PolicyNames.BuildingManagement)]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public DocumentsController(ApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // GET: Сваляне на прикачен документ (в момента - само на управляваната сграда)
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.Building.ManagerId == managerId);

        if (document == null)
            return NotFound(new { message = "Документът не е намерен." });

        var stream = await _fileStorage.OpenReadAsync(document.FilePath);
        if (stream == null)
            return NotFound(new { message = "Файлът липсва в хранилището." });

        return File(stream, "application/octet-stream", document.FileName);
    }
}

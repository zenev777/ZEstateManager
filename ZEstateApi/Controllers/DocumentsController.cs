// DocumentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;

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

    // GET: Сваляне на прикачен документ - домоуправителят винаги, а обикновен член
    // на сградата само ако документът е с достъп "All" (напр. протокол от събрание).
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var document = await _context.Documents.Include(d => d.Building).FirstOrDefaultAsync(d => d.Id == id);
        if (document == null)
            return NotFound(new { message = "Документът не е намерен." });

        var isManager = document.Building.ManagerId == userId;
        if (!isManager)
        {
            var isBuildingMember = await _context.ApartmentUsers
                .AnyAsync(au => au.UserId == userId && au.Apartment.BuildingId == document.BuildingId);

            if (!isBuildingMember || document.Access == DocumentAccess.ManagerOnly)
                return NotFound(new { message = "Документът не е намерен." });
        }

        var stream = await _fileStorage.OpenReadAsync(document.FilePath);
        if (stream == null)
            return NotFound(new { message = "Файлът липсва в хранилището." });

        return File(stream, "application/octet-stream", document.FileName);
    }
}

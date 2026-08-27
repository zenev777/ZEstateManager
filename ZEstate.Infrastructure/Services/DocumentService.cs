using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Documents;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public DocumentService(ApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<List<DocumentSummaryDto>> GetDocumentsAsync(string userId, string? type, DateTime? from, DateTime? to)
    {
        var building = await GetMyBuildingOrThrowAsync(userId);
        var isManager = building.ManagerId == userId;

        var query = _context.Documents.Where(d => d.BuildingId == building.Id);

        if (!isManager)
            query = query.Where(d => d.Access == DocumentAccess.All);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<DocumentType>(type, true, out var parsedType))
                throw new BadRequestException("Невалидна категория.");

            query = query.Where(d => d.Type == parsedType);
        }

        if (from.HasValue)
            query = query.Where(d => d.UploadedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(d => d.UploadedAt <= to.Value);

        var documents = await query.OrderByDescending(d => d.UploadedAt).ToListAsync();

        return documents.Select(d => new DocumentSummaryDto
        {
            Id = d.Id,
            FileName = d.FileName,
            Type = (int)d.Type,
            Access = (int)d.Access,
            UploadedAt = d.UploadedAt,
            RepairId = d.RepairId,
            MeetingId = d.MeetingId
        }).ToList();
    }

    public async Task<UploadedDocumentSummaryDto> UploadDocumentAsync(
        string managerId, Stream content, string fileName, string? contentType, long length, string type, string access)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        var validationError = DocumentUploadValidation.Validate(length, fileName, contentType);
        if (validationError != null)
            throw new BadRequestException(validationError);

        if (!Enum.TryParse<DocumentType>(type, true, out var documentType))
            throw new BadRequestException("Невалидна категория. Позволени: Protocol, Contract, Invoice, Other.");

        if (!Enum.TryParse<DocumentAccess>(access, true, out var documentAccess))
            throw new BadRequestException("Невалиден достъп. Позволени: All, ManagerOnly.");

        var storagePath = await _fileStorage.SaveAsync(content, fileName);

        var document = new Document
        {
            BuildingId = building.Id,
            FilePath = storagePath,
            FileName = fileName,
            Type = documentType,
            Access = documentAccess
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return new UploadedDocumentSummaryDto
        {
            Id = document.Id,
            FileName = document.FileName,
            Type = (int)document.Type,
            Access = (int)document.Access,
            UploadedAt = document.UploadedAt
        };
    }

    public async Task DeleteDocumentAsync(string managerId, int documentId)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == managerId);
        if (building == null)
            throw new NotFoundException("Нямаш управлявана сграда.");

        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.BuildingId == building.Id);
        if (document == null)
            throw new NotFoundException("Документът не е намерен.");

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        _fileStorage.Delete(document.FilePath);
    }

    public async Task<DocumentDownloadResult> DownloadAsync(string userId, int documentId)
    {
        var document = await _context.Documents.Include(d => d.Building).FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new NotFoundException("Документът не е намерен.");

        var isManager = document.Building.ManagerId == userId;
        if (!isManager)
        {
            var isBuildingMember = await _context.ApartmentUsers
                .AnyAsync(au => au.UserId == userId && au.Apartment.BuildingId == document.BuildingId);

            if (!isBuildingMember || document.Access == DocumentAccess.ManagerOnly)
                throw new NotFoundException("Документът не е намерен.");
        }

        var stream = await _fileStorage.OpenReadAsync(document.FilePath);
        if (stream == null)
            throw new NotFoundException("Файлът липсва в хранилището.");

        return new DocumentDownloadResult(stream, document.FileName);
    }

    private async Task<Building> GetMyBuildingOrThrowAsync(string userId)
    {
        var managed = await _context.Buildings.FirstOrDefaultAsync(b => b.ManagerId == userId);
        if (managed != null)
            return managed;

        var buildingId = await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        var building = buildingId.HasValue
            ? await _context.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId.Value)
            : null;

        if (building == null)
            throw new NotFoundException("Нямаш сграда.");

        return building;
    }
}

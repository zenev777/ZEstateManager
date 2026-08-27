using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Chat;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class ChatService : IChatService
{
    private const int HistoryLimit = 50;

    private readonly ApplicationDbContext _context;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ChatMessageResponseDto>> GetMessagesAsync(string userId)
    {
        var buildingId = await GetMyBuildingIdOrThrowAsync(userId, "Нямаш сграда.");

        var messages = await _context.ChatMessages
            .Where(m => m.BuildingId == buildingId)
            .Include(m => m.User)
            .OrderByDescending(m => m.SentAt)
            .Take(HistoryLimit)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return messages.Select(ToDto).ToList();
    }

    public async Task<ChatMessageSent> SendMessageAsync(string userId, string message)
    {
        var buildingId = await GetMyBuildingIdOrThrowAsync(userId, "Нямаш сграда.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new UnauthorizedException("Потребителят не е намерен.");

        var chatMessage = new ChatMessage
        {
            BuildingId = buildingId,
            UserId = userId,
            Message = message.Trim()
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var dto = new ChatMessageResponseDto
        {
            Id = chatMessage.Id,
            Message = chatMessage.Message,
            SentAt = chatMessage.SentAt,
            SenderId = userId,
            SenderName = user.Name
        };

        return new ChatMessageSent(dto, buildingId);
    }

    public async Task<ChatMessageDeleted> DeleteMessageAsync(string managerId, int messageId)
    {
        var buildingId = await GetMyBuildingIdOrThrowAsync(managerId, "Нямаш управлявана сграда.");

        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.BuildingId == buildingId);
        if (message == null)
            throw new NotFoundException("Съобщението не е намерено.");

        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();

        return new ChatMessageDeleted(buildingId);
    }

    private static ChatMessageResponseDto ToDto(ChatMessage m) => new()
    {
        Id = m.Id,
        Message = m.Message,
        SentAt = m.SentAt,
        SenderId = m.UserId,
        SenderName = m.User.Name
    };

    private async Task<int> GetMyBuildingIdOrThrowAsync(string userId, string notFoundMessage)
    {
        var managed = await _context.Buildings
            .Where(b => b.ManagerId == userId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        var buildingId = managed ?? await _context.ApartmentUsers
            .Where(au => au.UserId == userId)
            .Select(au => (int?)au.Apartment.BuildingId)
            .FirstOrDefaultAsync();

        if (buildingId == null)
            throw new NotFoundException(notFoundMessage);

        return buildingId.Value;
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZEstate.Core.DTOs.Chat;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class ChatService : IChatService
{
    private const int HistoryLimit = 50;

    // Precedence when a user holds more than one Identity role - the most
    // "senior" role is what's shown next to their name in chat.
    private static readonly string[] RolePrecedence =
    {
        RoleNames.HouseManager, RoleNames.Cashier, RoleNames.Administrator, RoleNames.Resident
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        var roleBySenderId = new Dictionary<string, string>();
        var dtos = new List<ChatMessageResponseDto>();
        foreach (var message in messages)
        {
            if (!roleBySenderId.TryGetValue(message.UserId, out var role))
            {
                role = await GetPrimaryRoleAsync(message.User);
                roleBySenderId[message.UserId] = role;
            }

            dtos.Add(ToDto(message, role));
        }

        return dtos;
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
            SenderName = user.Name,
            SenderRole = await GetPrimaryRoleAsync(user)
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

    private static ChatMessageResponseDto ToDto(ChatMessage m, string senderRole) => new()
    {
        Id = m.Id,
        Message = m.Message,
        SentAt = m.SentAt,
        SenderId = m.UserId,
        SenderName = m.User.Name,
        SenderRole = senderRole
    };

    private async Task<string> GetPrimaryRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return RolePrecedence.FirstOrDefault(roles.Contains) ?? string.Empty;
    }

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

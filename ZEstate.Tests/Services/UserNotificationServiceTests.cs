using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ZEstate.Core.DTOs.Notifications;
using ZEstate.Core.Exceptions;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
using ZEstate.Infrastructure.Services;

namespace ZEstate.Tests.Services;

// Uses a Sqlite-backed context (not the InMemory provider) because MarkAllAsReadAsync
// relies on ExecuteUpdateAsync, which InMemory can't translate.
public class UserNotificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly UserNotificationService _service;
    private const string UserId = "u1";

    public UserNotificationServiceTests()
    {
        _context = TestHelpers.CreateSqliteContext(out _connection);
        _userManager = TestHelpers.MockUserManager();
        _service = new UserNotificationService(_context, _userManager.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // Notification.UserId is a real FK under Sqlite (unlike the InMemory provider),
    // so the referenced users need to exist first.
    private void SeedUsers(params string[] userIds)
    {
        foreach (var id in userIds)
            _context.Users.Add(new ApplicationUser { Id = id, UserName = $"{id}@b.com", Email = $"{id}@b.com" });

        _context.SaveChanges();
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsOnlyOwnNotificationsNewestFirst()
    {
        SeedUsers(UserId, "other");
        _context.Notifications.AddRange(
            new Notification { UserId = UserId, Title = "A", Message = "a", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new Notification { UserId = UserId, Title = "B", Message = "b", CreatedAt = DateTime.UtcNow },
            new Notification { UserId = "other", Title = "C", Message = "c" });
        await _context.SaveChangesAsync();

        var result = await _service.GetMyNotificationsAsync(UserId);

        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0].Title);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnread()
    {
        SeedUsers(UserId);
        _context.Notifications.AddRange(
            new Notification { UserId = UserId, Title = "A", Message = "a", IsRead = true },
            new Notification { UserId = UserId, Title = "B", Message = "b", IsRead = false },
            new Notification { UserId = UserId, Title = "C", Message = "c", IsRead = false });
        await _context.SaveChangesAsync();

        var count = await _service.GetUnreadCountAsync(UserId);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAsReadAsync_NotFound_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkAsReadAsync(UserId, 999));
    }

    [Fact]
    public async Task MarkAsReadAsync_OtherUsersNotification_ThrowsNotFound()
    {
        SeedUsers("other");
        var notification = new Notification { UserId = "other", Title = "A", Message = "a" };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkAsReadAsync(UserId, notification.Id));
    }

    [Fact]
    public async Task MarkAllAsReadAsync_OnlyAffectsOwnUnread()
    {
        SeedUsers(UserId, "other");
        _context.Notifications.AddRange(
            new Notification { UserId = UserId, Title = "A", Message = "a", IsRead = false },
            new Notification { UserId = "other", Title = "B", Message = "b", IsRead = false });
        await _context.SaveChangesAsync();

        await _service.MarkAllAsReadAsync(UserId);

        // ExecuteUpdateAsync writes straight to the database, bypassing the change
        // tracker - re-query untracked so we see the actual persisted values.
        var reloaded = await _context.Notifications.AsNoTracking().ToListAsync();
        Assert.True(reloaded.Single(n => n.UserId == UserId).IsRead);
        Assert.False(reloaded.Single(n => n.UserId == "other").IsRead);
    }

    [Fact]
    public async Task GetPreferencesAsync_UnknownUser_ThrowsNotFound()
    {
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetPreferencesAsync(UserId));
    }

    [Fact]
    public async Task UpdatePreferencesAsync_PersistsFlagViaUserManager()
    {
        var user = new ApplicationUser { Id = UserId, EmailNotificationsEnabled = true };
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _service.UpdatePreferencesAsync(UserId, new NotificationPreferencesDto { EmailEnabled = false });

        Assert.False(result.EmailEnabled);
        Assert.False(user.EmailNotificationsEnabled);
        _userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }
}

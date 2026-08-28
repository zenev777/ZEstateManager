using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ZEstate.Core.DTOs.Auth;
using ZEstate.Core.Exceptions;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

namespace ZEstate.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _configuration = configuration;
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new UnauthorizedException("Грешен имейл или парола.");

        var token = await GenerateJwtToken(user);

        return new AuthResponseDto
        {
            Token = token,
            Email = user.Email!,
            Name = user.Name,
            Roles = await _userManager.GetRolesAsync(user)
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            throw new BadRequestException("Имейлът вече е зает.");

        if (dto.Role == "HouseManager" && dto.Building == null)
            throw new BadRequestException("Домоуправителят трябва да създаде сграда.");

        if (dto.Role == "Resident" && dto.JoinBuilding == null)
            throw new BadRequestException("Живущият трябва да въведе код за сграда.");

        Building? joinTargetBuilding = null;
        if (dto.Role == "Resident" && dto.JoinBuilding != null)
        {
            joinTargetBuilding = await _context.Buildings
                .FirstOrDefaultAsync(b => b.InviteCode == dto.JoinBuilding.InviteCode);

            if (joinTargetBuilding == null)
                throw new BadRequestException("Невалиден код за сграда.");

            var inviteCodeError = GetInviteCodeError(joinTargetBuilding);
            if (inviteCodeError != null)
                throw new BadRequestException(inviteCodeError);

            if (!Enum.TryParse<ApartmentRole>(dto.JoinBuilding.Status, true, out _))
                throw new BadRequestException("Невалиден статут.");
        }

        var user = new ApplicationUser
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            UserName = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, dto.Role);

        string? buildingInviteCode = null;

        // Сценарий 1: Домоуправител → създава сграда (+ евентуално собствен апартамент)
        if (dto.Role == "HouseManager" && dto.Building != null)
        {
            var building = new Building
            {
                Name = dto.Building.Name,
                Address = dto.Building.Address,
                InviteCode = GenerateInviteCode(),
                ManagerId = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Buildings.Add(building);
            await _context.SaveChangesAsync();

            if (dto.Building.LivesInBuilding && !string.IsNullOrWhiteSpace(dto.Building.ApartmentNumber))
            {
                var apartment = new Apartment
                {
                    BuildingId = building.Id,
                    Number = dto.Building.ApartmentNumber,
                    Floor = dto.Building.Floor ?? 0,
                    IdealParts = 0,
                    Budget = 0
                };

                _context.Apartments.Add(apartment);
                await _context.SaveChangesAsync();

                _context.ApartmentUsers.Add(new ApartmentUser
                {
                    ApartmentId = apartment.Id,
                    UserId = user.Id,
                    Role = ApartmentRole.HouseManager
                });

                await _context.SaveChangesAsync();
            }

            buildingInviteCode = building.InviteCode;
        }

        // Сценарий 2: Живущ → проверява кода и изпраща заявка за присъединяване
        if (dto.Role == "Resident" && dto.JoinBuilding != null && joinTargetBuilding != null)
        {
            var apartment = await _context.Apartments
                .FirstOrDefaultAsync(a => a.BuildingId == joinTargetBuilding.Id
                                        && a.Number == dto.JoinBuilding.ApartmentNumber);

            if (apartment == null)
            {
                apartment = new Apartment
                {
                    BuildingId = joinTargetBuilding.Id,
                    Number = dto.JoinBuilding.ApartmentNumber,
                    Floor = 0,
                    IdealParts = 0,
                    Budget = 0
                };

                _context.Apartments.Add(apartment);
                await _context.SaveChangesAsync();
            }

            Enum.TryParse<ApartmentRole>(dto.JoinBuilding.Status, true, out var requestedRole);

            _context.JoinRequests.Add(new JoinRequest
            {
                UserId = user.Id,
                BuildingId = joinTargetBuilding.Id,
                ApartmentId = apartment.Id,
                RequestedRole = requestedRole,
                Notes = dto.JoinBuilding.Notes,
                Status = JoinRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            joinTargetBuilding.InviteCodeUseCount++;

            await _context.SaveChangesAsync();
        }

        var token = await GenerateJwtToken(user);

        return new AuthResponseDto
        {
            Token = token,
            Email = user.Email!,
            Name = user.Name,
            Roles = await _userManager.GetRolesAsync(user),
            BuildingInviteCode = buildingInviteCode
        };
    }

    public async Task<BuildingByCodeDto> GetBuildingByCodeAsync(string code)
    {
        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.InviteCode == code);
        if (building == null)
            throw new NotFoundException("Невалиден код.");

        var inviteCodeError = GetInviteCodeError(building);
        if (inviteCodeError != null)
            throw new BadRequestException(inviteCodeError);

        return new BuildingByCodeDto
        {
            Id = building.Id,
            Name = building.Name,
            Address = building.Address,
            InviteCode = building.InviteCode
        };
    }

    public async Task<MeResponseDto> GetMeAsync(string userId, bool isManager)
    {
        if (isManager)
            return new MeResponseDto { Role = "HouseManager" };

        // An active apartment membership means the user is already a confirmed
        // member, regardless of how it came to be - through the normal approved
        // join-request flow, or seeded/added directly. Check this first rather than
        // only trusting join-request history, which won't exist in the latter case.
        var activeMembership = await _context.ApartmentUsers
            .Where(au => au.UserId == userId && au.IsActive)
            .Include(au => au.Apartment).ThenInclude(a => a.Building)
            .FirstOrDefaultAsync();

        if (activeMembership != null)
        {
            return new MeResponseDto
            {
                Role = "Resident",
                MembershipStatus = "Approved",
                BuildingName = activeMembership.Apartment.Building.Name,
                ApartmentNumber = activeMembership.Apartment.Number
            };
        }

        var joinRequests = await _context.JoinRequests
            .Where(jr => jr.UserId == userId)
            .Include(jr => jr.Building)
            .Include(jr => jr.Apartment)
            .OrderByDescending(jr => jr.CreatedAt)
            .ToListAsync();

        var latest = joinRequests.FirstOrDefault();
        if (latest == null)
            return new MeResponseDto { Role = "Resident", MembershipStatus = "None" };

        var canRetry = latest.Status == JoinRequestStatus.Rejected && joinRequests.Count < 2;

        return new MeResponseDto
        {
            Role = "Resident",
            MembershipStatus = latest.Status.ToString(),
            BuildingName = latest.Building.Name,
            ApartmentNumber = latest.Apartment.Number,
            CanRetry = canRetry,
            RejectionReason = latest.RejectionReason
        };
    }

    public async Task ResubmitJoinRequestAsync(string userId, JoinBuildingDto dto)
    {
        var existingRequests = await _context.JoinRequests
            .Where(jr => jr.UserId == userId)
            .ToListAsync();

        var latest = existingRequests.OrderByDescending(jr => jr.CreatedAt).FirstOrDefault();
        if (latest == null || latest.Status != JoinRequestStatus.Rejected || existingRequests.Count >= 2)
            throw new BadRequestException("Няма възможност за нова заявка.");

        var building = await _context.Buildings.FirstOrDefaultAsync(b => b.InviteCode == dto.InviteCode);
        if (building == null)
            throw new BadRequestException("Невалиден код за сграда.");

        var inviteCodeError = GetInviteCodeError(building);
        if (inviteCodeError != null)
            throw new BadRequestException(inviteCodeError);

        if (!Enum.TryParse<ApartmentRole>(dto.Status, true, out var requestedRole))
            throw new BadRequestException("Невалиден статут.");

        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(a => a.BuildingId == building.Id && a.Number == dto.ApartmentNumber);

        if (apartment == null)
        {
            apartment = new Apartment
            {
                BuildingId = building.Id,
                Number = dto.ApartmentNumber,
                Floor = 0,
                IdealParts = 0,
                Budget = 0
            };

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync();
        }

        _context.JoinRequests.Add(new JoinRequest
        {
            UserId = userId,
            BuildingId = building.Id,
            ApartmentId = apartment.Id,
            RequestedRole = requestedRole,
            Notes = dto.Notes,
            Status = JoinRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);

        // Always no-op silently if the email doesn't exist / is inactive - the
        // controller returns the same response either way, so we don't reveal it.
        if (user == null || !user.IsActive)
            return;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
        var resetLink = $"{frontendBaseUrl}/reset-password" +
                         $"?email={Uri.EscapeDataString(email)}" +
                         $"&token={Uri.EscapeDataString(token)}";

        var body = $"""
            <p>Здравей, {user.FirstName}!</p>
            <p>Получихме заявка за нулиране на паролата на профила ти в ZEstate.
            Ако не си я направил/а ти, просто игнорирай този имейл.</p>
            <p><a href="{resetLink}">Нулирай паролата си</a></p>
            <p>Линкът е валиден за ограничено време и може да се използва само веднъж.</p>
            """;

        await _emailSender.SendAsync(user.Email!, "Нулиране на парола — ZEstate", body);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            throw new BadRequestException("Невалиден или изтекъл линк за нулиране на паролата.");

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            var isInvalidToken = result.Errors.Any(e => e.Code == "InvalidToken");
            var message = isInvalidToken
                ? "Невалиден или изтекъл линк за нулиране на паролата."
                : string.Join(" ", result.Errors.Select(e => e.Description));
            throw new BadRequestException(message);
        }
    }

    // Checks whether the invite code is still active, unexpired, and under its usage limit
    private static string? GetInviteCodeError(Building building)
    {
        if (!building.InviteCodeActive)
            return "Кодът за покана е анулиран от домоуправителя.";

        if (building.InviteCodeExpiresAt.HasValue && building.InviteCodeExpiresAt.Value < DateTime.UtcNow)
            return "Кодът за покана е изтекъл.";

        if (building.InviteCodeMaxUses.HasValue && building.InviteCodeUseCount >= building.InviteCodeMaxUses.Value)
            return "Кодът за покана е достигнал лимита си на използване.";

        return null;
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.Name),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ZEstate.Core.DTOs.Auth;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.Enums;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _configuration = configuration;
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Грешен имейл или парола." });

        var token = await GenerateJwtToken(user);

        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email!,
            Name = user.Name,
            Roles = await _userManager.GetRolesAsync(user)
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Проверка за съществуващ имейл
        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { message = "Имейлът вече е зает." });

        // Валидация по роля
        if (dto.Role == "HouseManager" && dto.Building == null)
            return BadRequest(new { message = "Домоуправителят трябва да създаде сграда." });

        if (dto.Role == "Resident" && dto.JoinBuilding == null)
            return BadRequest(new { message = "Живущият трябва да въведе код за сграда." });

        Building? joinTargetBuilding = null;
        if (dto.Role == "Resident" && dto.JoinBuilding != null)
        {
            joinTargetBuilding = await _context.Buildings
                .FirstOrDefaultAsync(b => b.InviteCode == dto.JoinBuilding.InviteCode);

            if (joinTargetBuilding == null)
                return BadRequest(new { message = "Невалиден код за сграда." });

            if (!Enum.TryParse<ApartmentRole>(dto.JoinBuilding.Status, true, out _))
                return BadRequest(new { message = "Невалиден статут." });
        }

        // Създаване на потребителя
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
            return BadRequest(result.Errors.Select(e => e.Description));

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

            await _context.SaveChangesAsync();
        }

        var token = await GenerateJwtToken(user);

        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email!,
            Name = user.Name,
            Roles = await _userManager.GetRolesAsync(user),
            BuildingInviteCode = buildingInviteCode
        });
    }

    // GET: Информация за сграда по код (за живущите, преди регистрация)
    [HttpGet("building-by-code/{code}")]
    public async Task<IActionResult> GetBuildingByCode(string code)
    {
        var building = await _context.Buildings
            .FirstOrDefaultAsync(b => b.InviteCode == code);

        if (building == null)
            return NotFound(new { message = "Невалиден код." });

        return Ok(new
        {
            building.Id,
            building.Name,
            building.Address,
            building.InviteCode
        });
    }

    // GET: Статус на текущия потребител — за резидента показва статуса на заявката му
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isManager = User.IsInRole("HouseManager");

        if (isManager)
            return Ok(new { role = "HouseManager" });

        var joinRequests = await _context.JoinRequests
            .Where(jr => jr.UserId == userId)
            .Include(jr => jr.Building)
            .Include(jr => jr.Apartment)
            .OrderByDescending(jr => jr.CreatedAt)
            .ToListAsync();

        var latest = joinRequests.FirstOrDefault();
        if (latest == null)
            return Ok(new { role = "Resident", membershipStatus = "None" });

        var canRetry = latest.Status == JoinRequestStatus.Rejected && joinRequests.Count < 2;

        return Ok(new
        {
            role = "Resident",
            membershipStatus = latest.Status.ToString(),
            buildingName = latest.Building.Name,
            apartmentNumber = latest.Apartment.Number,
            canRetry
        });
    }

    // POST: Единствен позволен повторен опит за живущ, чиято заявка е отхвърлена
    [HttpPost("resubmit-join-request")]
    [Authorize(Roles = "Resident")]
    public async Task<IActionResult> ResubmitJoinRequest([FromBody] JoinBuildingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var existingRequests = await _context.JoinRequests
            .Where(jr => jr.UserId == userId)
            .ToListAsync();

        var latest = existingRequests.OrderByDescending(jr => jr.CreatedAt).FirstOrDefault();
        if (latest == null || latest.Status != JoinRequestStatus.Rejected || existingRequests.Count >= 2)
            return BadRequest(new { message = "Няма възможност за нова заявка." });

        var building = await _context.Buildings
            .FirstOrDefaultAsync(b => b.InviteCode == dto.InviteCode);

        if (building == null)
            return BadRequest(new { message = "Невалиден код за сграда." });

        if (!Enum.TryParse<ApartmentRole>(dto.Status, true, out var requestedRole))
            return BadRequest(new { message = "Невалиден статут." });

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
        return Ok(new { message = "Заявката е изпратена отново." });
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

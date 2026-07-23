// AuthController.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        // Създаване на потребителя
        var user = new ApplicationUser
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            UserName = dto.Email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRoleAsync(user, dto.Role);

        // Сценарий 1: Домоуправител → създава сграда + апартаменти
        if (dto.Role == "HouseManager" && dto.Building != null)
        {
            var building = new Building
            {
                Name = dto.Building.Name,
                Address = dto.Building.Address,
                InviteCode = GenerateInviteCode(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Buildings.Add(building);
            await _context.SaveChangesAsync();

            // Автоматично генериране на апартаменти
            //for (int i = 1; i <= dto.Building.ApartmentsCount; i++)
            //{
            //    _context.Apartments.Add(new Apartment
            //    {
            //        BuildingId = building.Id,
            //        Number = i.ToString(),
            //        Floor = (int)Math.Ceiling((double)i / (dto.Building.ApartmentsCount / dto.Building.FloorsCount)),
            //        IdealParts = Math.Round(100m / dto.Building.ApartmentsCount, 2),
            //        Budget = 0
            //    });
            //}

            // Свързваме домоуправителя към сградата
            _context.ApartmentUsers.Add(new ApartmentUser
            {
                UserId = user.Id,
                BuildingId = building.Id,
                Role = ApartmentRole.HouseManager
            });

            await _context.SaveChangesAsync();
        }

        // Сценарий 2: Живущ → проверява кода и изпраща заявка
        if (dto.Role == "Resident" && dto.JoinBuilding != null)
        {
            var building = await _context.Buildings
                .FirstOrDefaultAsync(b => b.InviteCode == dto.JoinBuilding.InviteCode);

            if (building == null)
                return BadRequest(new { message = "Невалиден код за сграда." });

            //var apartment = await _context.Apartments
            //    .FirstOrDefaultAsync(a => a.Id == dto.JoinBuilding.ApartmentId
            //                           && a.BuildingId == building.Id);

            //if (apartment == null)
            //    return BadRequest(new { message = "Апартаментът не е намерен." });

            _context.JoinRequests.Add(new JoinRequest
            {
                UserId = user.Id,
                BuildingId = building.Id,
                ApartmentId = apartment.Id,
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
            Roles = await _userManager.GetRolesAsync(user)
        });
    }

    // GET: Апартаменти по код на сграда (за живущите)
    [HttpGet("building-by-code/{code}")]
    public async Task<IActionResult> GetBuildingByCode(string code)
    {
        var building = await _context.Buildings
            .Include(b => b.Apartments)
            .FirstOrDefaultAsync(b => b.InviteCode == code);

        if (building == null)
            return NotFound(new { message = "Невалиден код." });

        return Ok(new
        {
            building.Id,
            building.Name,
            building.Address,
            Apartments = building.Apartments.Select(a => new
            {
                a.Id,
                a.Number,
                a.Floor
            }).OrderBy(a => a.Floor).ThenBy(a => a.Number)
        });
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
// AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZEstate.Core.DTOs.Auth;
using ZEstate.Core.Interfaces;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _authService.LoginAsync(dto));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return Ok(await _authService.RegisterAsync(dto));
    }

    // GET: Информация за сграда по код (за живущите, преди регистрация)
    [HttpGet("building-by-code/{code}")]
    public async Task<IActionResult> GetBuildingByCode(string code)
    {
        return Ok(await _authService.GetBuildingByCodeAsync(code));
    }

    // GET: Статус на текущия потребител — за резидента показва статуса на заявката му
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isManager = User.IsInRole("HouseManager");

        return Ok(await _authService.GetMeAsync(userId, isManager));
    }

    // POST: Единствен позволен повторен опит за живущ, чиято заявка е отхвърлена
    [HttpPost("resubmit-join-request")]
    [Authorize(Roles = "Resident")]
    public async Task<IActionResult> ResubmitJoinRequest([FromBody] JoinBuildingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _authService.ResubmitJoinRequestAsync(userId, dto);

        return Ok(new { message = "Заявката е изпратена отново." });
    }

    // POST: Sends an email with a password-reset link if the email exists
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.ForgotPasswordAsync(dto.Email);

        return Ok(new { message = "Ако имейлът съществува в системата, ще получиш линк за нулиране на паролата." });
    }

    // POST: Sets a new password using the link from forgot-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.ResetPasswordAsync(dto);

        return Ok(new { message = "Паролата е сменена успешно." });
    }
}

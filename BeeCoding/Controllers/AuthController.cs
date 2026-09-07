using System.Security.Claims;
using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordService _pw;

    public AuthController(AppDbContext db, PasswordService pw)
    {
        _db = db;
        _pw = pw;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<MeDto>> Register(RegisterDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length < 3 || !email.Contains('@')) return BadRequest("Invalid email.");
        if ((dto.Password ?? "").Length < 6) return BadRequest("Password must be at least 6 characters.");
        if (string.IsNullOrWhiteSpace(dto.DisplayName)) return BadRequest("Display name is required.");

        var role = dto.Role?.Equals("Teacher", StringComparison.OrdinalIgnoreCase) == true
            ? UserRole.Teacher : UserRole.Student;

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("An account with that email already exists.");

        var user = new User
        {
            Email = email,
            DisplayName = dto.DisplayName.Trim(),
            Role = role,
        };
        user.PasswordHash = _pw.Hash(user, dto.Password!);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInAsync(user);
        return new MeDto(user.Id, user.Email, user.DisplayName, user.Role.ToString());
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<MeDto>> Login(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !_pw.Verify(user, dto.Password ?? ""))
            return Unauthorized("Wrong email or password.");

        await SignInAsync(user);
        return new MeDto(user.Id, user.Email, user.DisplayName, user.Role.ToString());
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeDto>> Me()
    {
        var user = await _db.Users.FindAsync(UserId);
        if (user is null) return Unauthorized();
        return new MeDto(user.Id, user.Email, user.DisplayName, user.Role.ToString());
    }

    private async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}

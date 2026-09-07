using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

public record LeaderRowDto(int Rank, int UserId, string DisplayName, string Role, int Xp, int Level, bool Me);

[ApiController]
[Authorize]
public class ProgressController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ProgressService _progress;

    public ProgressController(AppDbContext db, ProgressService progress)
    {
        _db = db;
        _progress = progress;
    }

    [HttpGet("api/me/progress")]
    public Task<ProgressDto> Mine() => _progress.GetAsync(UserId);

    [HttpGet("api/leaderboard")]
    public async Task<ActionResult<IEnumerable<LeaderRowDto>>> Leaderboard([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        var top = await _db.Users
            .Where(u => u.Xp > 0)
            .OrderByDescending(u => u.Xp).ThenBy(u => u.Id)
            .Take(limit)
            .Select(u => new { u.Id, u.DisplayName, u.Role, u.Xp })
            .ToListAsync();

        return top.Select((u, i) => new LeaderRowDto(
            i + 1, u.Id, u.DisplayName, u.Role.ToString(), u.Xp,
            ProgressService.LevelForXp(u.Xp), u.Id == UserId)).ToList();
    }
}

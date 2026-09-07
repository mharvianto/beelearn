using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Controllers;

/// <summary>
/// Free practice: any signed-in user can browse and solve every public bank problem,
/// independent of any board. Solving a problem for the first time awards XP.
/// </summary>
[ApiController]
[Authorize]
[Route("api/practice")]
public class PracticeController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly JudgeQueue _queue;
    private readonly RateLimiter _rate;

    public PracticeController(AppDbContext db, JudgeQueue queue, RateLimiter rate)
    {
        _db = db;
        _queue = queue;
        _rate = rate;
    }

    private IQueryable<BankProblem> Pool() => _db.BankProblems.Where(b => b.IsPublic);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PracticeSummaryDto>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? level, [FromQuery] string? status)
    {
        var query = Pool();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var n = q.Trim();
            query = query.Where(b => EF.Functions.Like(b.Title, $"%{n}%") || EF.Functions.Like(b.Tags, $"%{n}%"));
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var t = tag.Trim().ToLowerInvariant();
            query = query.Where(b => EF.Functions.Like(b.Tags, $"%{t}%"));
        }
        if (Enum.TryParse<ProblemLevel>(level, ignoreCase: true, out var lvl))
            query = query.Where(b => b.Level == lvl);

        var problems = await query
            .OrderBy(b => b.Level).ThenBy(b => b.Title)
            .Select(b => new { b.Id, b.Title, b.Language, b.Level, b.Tags })
            .Take(500)
            .ToListAsync();
        var ids = problems.Select(p => p.Id).ToList();

        // best verdict/score per problem for this user
        var mine = await _db.BankSubmissions
            .Where(s => s.UserId == UserId && ids.Contains(s.BankProblemId) && s.Status == SubmissionStatus.Done)
            .GroupBy(s => s.BankProblemId)
            .Select(g => new
            {
                BankProblemId = g.Key,
                Best = g.Max(x => x.Score),
                Solved = g.Any(x => x.Verdict == Verdict.Accepted && x.Score >= 1.0),
                Latest = g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First().Verdict,
            })
            .ToListAsync();
        var byId = mine.ToDictionary(m => m.BankProblemId);

        var list = problems.Select(p =>
        {
            byId.TryGetValue(p.Id, out var m);
            return new PracticeSummaryDto(
                p.Id, p.Title, p.Language, p.Level.ToString(), p.Tags,
                m?.Latest.ToString() ?? "None",
                m?.Best ?? 0,
                m?.Solved ?? false);
        });

        list = status?.ToLowerInvariant() switch
        {
            "solved" => list.Where(x => x.Solved),
            "unsolved" => list.Where(x => !x.Solved),
            "attempted" => list.Where(x => x.MyVerdict != "None" && !x.Solved),
            _ => list,
        };
        return list.ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PracticeProblemDto>> Get(int id)
    {
        var b = await Pool().Include(x => x.TestCases).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        bool solved = await _db.BankSubmissions.AnyAsync(
            s => s.UserId == UserId && s.BankProblemId == id && s.Verdict == Verdict.Accepted && s.Score >= 1.0);
        return Mapping.ToPracticeDto(b, solved);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<object>> Submit(int id, SubmitDto dto)
    {
        if (!await Pool().AnyAsync(b => b.Id == id)) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("Code is empty.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");
        if (!_rate.TryAcquire(UserId)) return StatusCode(429, "Slow down a moment and try again.");

        var sub = new BankSubmission { BankProblemId = id, UserId = UserId, Code = dto.Code };
        _db.BankSubmissions.Add(sub);
        await _db.SaveChangesAsync();

        await _queue.EnqueueAsync(new BankSubmissionJob(sub.Id));
        return Accepted(new { submissionId = sub.Id });
    }

    [HttpGet("{id:int}/submissions")]
    public async Task<ActionResult<IEnumerable<BankSubmissionDto>>> Mine(int id)
    {
        var rows = await _db.BankSubmissions
            .Where(s => s.BankProblemId == id && s.UserId == UserId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .ToListAsync();
        return rows.Select(s => Mapping.ToDto(s)).ToList();
    }

    [HttpGet("submissions/{sid:int}")]
    public async Task<ActionResult<BankSubmissionDto>> One(int sid)
    {
        var s = await _db.BankSubmissions.FirstOrDefaultAsync(x => x.Id == sid);
        if (s is null) return NotFound();
        if (s.UserId != UserId) return Forbid();
        return Mapping.ToDto(s);
    }
}

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
    public async Task<ActionResult<PracticePageDto>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? level, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

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
            .Take(2000)
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

        var all = list.ToList();
        var pageCount = Math.Max(1, (int)Math.Ceiling(all.Count / (double)pageSize));
        page = Math.Min(page, pageCount);
        var pageItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PracticePageDto(all.Count, all.Count(x => x.Solved), page, pageSize, pageItems);
    }

    private static IEnumerable<string> TagsOf(string? t) =>
        (t ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(x => x.ToLowerInvariant()).Distinct();

    /// <summary>
    /// "What should I do next?" for Practice: per-topic progress + up to 3 recommended
    /// unsolved problems, chosen to keep the difficulty ramp gentle (continue a topic
    /// you've started, one step up in difficulty, avoid problems you've bounced off).
    /// </summary>
    [HttpGet("guide")]
    public async Task<ActionResult<PracticeGuideDto>> Guide()
    {
        var problems = await Pool()
            .Select(b => new { b.Id, b.Title, b.Language, b.Level, b.Tags })
            .ToListAsync();
        if (problems.Count == 0) return new PracticeGuideDto(new(), new());

        var ids = problems.Select(p => p.Id).ToHashSet();
        var subs = await _db.BankSubmissions
            .Where(s => s.UserId == UserId && s.Status == SubmissionStatus.Done && ids.Contains(s.BankProblemId))
            .GroupBy(s => s.BankProblemId)
            .Select(g => new
            {
                Id = g.Key,
                Solved = g.Any(x => x.Verdict == Verdict.Accepted && x.Score >= 1.0),
                Fails = g.Count(x => !(x.Verdict == Verdict.Accepted && x.Score >= 1.0)),
            })
            .ToListAsync();
        var solvedSet = subs.Where(s => s.Solved).Select(s => s.Id).ToHashSet();
        var attemptedSet = subs.Select(s => s.Id).ToHashSet();
        var fails = subs.ToDictionary(s => s.Id, s => s.Fails);

        // ---- per-topic progress ----
        var byTag = new Dictionary<string, (int Total, int Solved, int Attempted)>();
        foreach (var p in problems)
            foreach (var tag in TagsOf(p.Tags))
            {
                var c = byTag.GetValueOrDefault(tag);
                byTag[tag] = (c.Total + 1,
                    c.Solved + (solvedSet.Contains(p.Id) ? 1 : 0),
                    c.Attempted + (attemptedSet.Contains(p.Id) ? 1 : 0));
            }

        var topics = byTag
            .Select(kv => new TopicProgressDto(kv.Key, kv.Value.Total, kv.Value.Solved, kv.Value.Attempted))
            .OrderByDescending(t => t.Attempted > 0)                          // topics you've started first
            .ThenBy(t => t.Total == 0 ? 1.0 : (double)t.Solved / t.Total)     // least complete first
            .ThenByDescending(t => t.Total)
            .Take(16)
            .ToList();

        // ---- target difficulty per topic: ramp up once the lower tier is mostly cleared ----
        var topicTarget = new Dictionary<string, int>();
        foreach (var tag in byTag.Keys)
        {
            var inTag = problems.Where(p => TagsOf(p.Tags).Contains(tag)).ToList();
            int solvedAt(int lvl) => inTag.Count(p => (int)p.Level == lvl && solvedSet.Contains(p.Id));
            int totalAt(int lvl) => inTag.Count(p => (int)p.Level == lvl);
            int target = 1;
            if (totalAt(1) > 0 && solvedAt(1) >= Math.Ceiling(totalAt(1) * 0.6)) target = 2;
            if (target == 2 && totalAt(2) > 0 && solvedAt(2) >= Math.Ceiling(totalAt(2) * 0.5)) target = 3;
            topicTarget[tag] = target;
        }

        var startedTopics = byTag
            .Where(kv => kv.Value.Attempted > 0 && kv.Value.Solved < kv.Value.Total)
            .Select(kv => kv.Key).ToHashSet();

        var recommended = problems
            .Where(p => !solvedSet.Contains(p.Id))
            .Select(p =>
            {
                var tags = TagsOf(p.Tags).ToList();
                bool inStarted = tags.Any(startedTopics.Contains);
                int target = tags.Where(topicTarget.ContainsKey).Select(t => topicTarget[t])
                                 .DefaultIfEmpty(1).Min();
                double score = (inStarted ? 100 : 0)
                             - 25 * Math.Abs((int)p.Level - target)
                             - 40 * Math.Max(0, fails.GetValueOrDefault(p.Id) - 1)
                             - 3 * ((int)p.Level - 1);
                var startedTag = tags.FirstOrDefault(startedTopics.Contains);
                string reason = attemptedSet.Contains(p.Id) ? "Give this another go"
                    : startedTag is not null ? $"Continue with {startedTag}"
                    : (int)p.Level == 1 ? "A gentle place to start"
                    : $"Try a {p.Level.ToString().ToLowerInvariant()} one";
                return (p, score, reason);
            })
            .OrderByDescending(x => x.score)
            .ThenBy(x => (int)x.p.Level).ThenBy(x => x.p.Title)
            .Take(3)
            .Select(x => new RecommendationDto(
                x.p.Id, x.p.Title, x.p.Language, x.p.Level.ToString(), x.p.Tags, x.reason))
            .ToList();

        return new PracticeGuideDto(topics, recommended);
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
        var problemLang = await Pool().Where(b => b.Id == id).Select(b => b.Language).FirstOrDefaultAsync();
        if (problemLang is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("Code is empty.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");
        if (!_rate.TryAcquire(UserId)) return StatusCode(429, "Slow down a moment and try again.");

        var sub = new BankSubmission
        {
            BankProblemId = id,
            UserId = UserId,
            Code = dto.Code,
            Language = dto.Language is "c" or "cpp" ? dto.Language : problemLang,
        };
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

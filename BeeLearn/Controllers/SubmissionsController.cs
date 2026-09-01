using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

[ApiController]
[Authorize]
public class SubmissionsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly VisibilityService _vis;
    private readonly JudgeQueue _queue;
    private readonly RateLimiter _rate;
    private readonly IBoardNotifier _notifier;

    public SubmissionsController(AppDbContext db, BoardService boards, VisibilityService vis,
        JudgeQueue queue, RateLimiter rate, IBoardNotifier notifier)
    {
        _db = db;
        _boards = boards;
        _vis = vis;
        _queue = queue;
        _rate = rate;
        _notifier = notifier;
    }

    [HttpPost("api/problems/{problemId:int}/submit")]
    public async Task<ActionResult<object>> Submit(int problemId, SubmitDto dto)
    {
        var problem = await _db.Problems.FirstOrDefaultAsync(p => p.Id == problemId);
        if (problem is null) return NotFound();

        var membership = await _boards.GetMembershipAsync(problem.BoardId, UserId);
        if (membership is null) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("Code is empty.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");
        if (!_rate.TryAcquire(UserId)) return StatusCode(429, "Slow down a moment and try again.");

        var sub = new Submission
        {
            ProblemId = problemId,
            UserId = UserId,
            Code = dto.Code,
            Status = SubmissionStatus.Queued,
        };
        _db.Submissions.Add(sub);

        // Upsert the wall post for (problem, student) so it exists as soon as they try.
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.ProblemId == problemId && p.UserId == UserId);
        if (post is null)
            _db.Posts.Add(new Post { BoardId = problem.BoardId, ProblemId = problemId, UserId = UserId });
        else
            post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _queue.EnqueueAsync(new SubmissionJob(sub.Id));
        return Accepted(new { submissionId = sub.Id });
    }

    [HttpGet("api/problems/{problemId:int}/submissions")]
    public async Task<ActionResult<IEnumerable<SubmissionDto>>> ListForProblem(int problemId)
    {
        var problem = await _db.Problems.FirstOrDefaultAsync(p => p.Id == problemId);
        if (problem is null) return NotFound();

        var board = await _db.Boards.Include(b => b.Members).ThenInclude(m => m.User)
            .FirstAsync(b => b.Id == problem.BoardId);
        var viewer = board.Members.FirstOrDefault(m => m.UserId == UserId);
        if (viewer is null) return Forbid();
        bool staff = _vis.IsStaff(viewer.Role);

        var subs = await _db.Submissions
            .Where(s => s.ProblemId == problemId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .ToListAsync();

        var membersById = board.Members.ToDictionary(m => m.UserId);
        var result = new List<SubmissionDto>();
        foreach (var s in subs)
        {
            if (s.UserId == UserId)
            {
                result.Add(Mapping.ToDto(s, UserId, canSeeCode: true, viewer.User!.DisplayName));
                continue;
            }
            if (!membersById.TryGetValue(s.UserId, out var author)) continue;
            if (!_vis.CanSeePeerRow(UserId, staff, board, author)) continue;
            bool full = _vis.CanSeePeerSubmission(UserId, staff, board, author, s);
            result.Add(Mapping.ToDto(s, UserId, full, author.User!.DisplayName));
        }
        return result;
    }

    [HttpGet("api/submissions/{id:int}")]
    public async Task<ActionResult<SubmissionDto>> Get(int id)
    {
        var s = await _db.Submissions.Include(x => x.Problem).Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        if (s.UserId == UserId)
            return Mapping.ToDto(s, UserId, canSeeCode: true, s.User!.DisplayName);

        var board = await _db.Boards.Include(b => b.Members).ThenInclude(m => m.User)
            .FirstAsync(b => b.Id == s.Problem!.BoardId);
        var viewer = board.Members.FirstOrDefault(m => m.UserId == UserId);
        if (viewer is null) return Forbid();
        bool staff = _vis.IsStaff(viewer.Role);

        var author = board.Members.FirstOrDefault(m => m.UserId == s.UserId);
        if (author is null || !_vis.CanSeePeerRow(UserId, staff, board, author)) return Forbid();
        bool full = _vis.CanSeePeerSubmission(UserId, staff, board, author, s);
        return Mapping.ToDto(s, UserId, full, author.User!.DisplayName);
    }

    /// <summary>Feature 4: a student hides/unhides their own submission from other students.</summary>
    [HttpPatch("api/submissions/{id:int}")]
    public async Task<ActionResult<SubmissionDto>> Update(int id, UpdateSubmissionDto dto)
    {
        var s = await _db.Submissions.Include(x => x.User).Include(x => x.Problem)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.UserId != UserId) return Forbid();

        s.HiddenByStudent = dto.HiddenByStudent;
        await _db.SaveChangesAsync();
        await _notifier.ProgressChangedAsync(s.Problem!.BoardId, s.ProblemId, s.UserId);
        return Mapping.ToDto(s, UserId, canSeeCode: true, s.User!.DisplayName);
    }
}

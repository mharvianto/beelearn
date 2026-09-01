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
public class WallController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly WallService _wall;
    private readonly VisibilityService _vis;
    private readonly IBoardNotifier _notifier;

    public WallController(AppDbContext db, WallService wall, VisibilityService vis, IBoardNotifier notifier)
    {
        _db = db;
        _wall = wall;
        _vis = vis;
        _notifier = notifier;
    }

    [HttpGet("api/boards/{boardId:int}/wall")]
    public async Task<ActionResult<WallDto>> Get(int boardId)
    {
        var wall = await _wall.BuildWallAsync(boardId, UserId);
        return wall is null ? Forbid() : wall;
    }

    [HttpPut("api/posts/{postId:int}/note")]
    public async Task<IActionResult> SetNote(int postId, NoteDto dto)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null) return NotFound();
        if (post.UserId != UserId) return Forbid();

        post.Note = (dto.Note ?? "").Trim();
        if (post.Note.Length > 500) post.Note = post.Note[..500];
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notifier.WallChangedAsync(post.BoardId);
        return NoContent();
    }

    [HttpPost("api/posts/{postId:int}/reactions")]
    public async Task<ActionResult<IEnumerable<ReactionDto>>> React(int postId, ReactDto dto)
    {
        var emoji = dto.Emoji ?? "";
        if (!WallService.AllowedEmojis.Contains(emoji)) return BadRequest("Unsupported reaction.");

        var (post, board, viewer, author, latest) = await _wall.LoadPostContextAsync(postId, UserId);
        if (post is null || board is null || viewer is null || author is null) return NotFound();
        bool staff = _vis.IsStaff(viewer.Role);
        if (!_wall.CanViewPost(board, UserId, staff, author, latest)) return Forbid();

        var existing = await _db.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == UserId && r.Emoji == emoji);
        if (existing is null)
            _db.PostReactions.Add(new PostReaction { PostId = postId, UserId = UserId, Emoji = emoji });
        else
            _db.PostReactions.Remove(existing);
        await _db.SaveChangesAsync();
        await _notifier.WallChangedAsync(post.BoardId);

        var reactions = await _db.PostReactions.Where(r => r.PostId == postId).ToListAsync();
        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new ReactionDto(g.Key, g.Count(), g.Any(r => r.UserId == UserId)))
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    [HttpPost("api/posts/{postId:int}/comments")]
    public async Task<ActionResult<CommentDto>> Comment(int postId, CommentBodyDto dto)
    {
        var body = (dto.Body ?? "").Trim();
        if (body.Length == 0) return BadRequest("Comment is empty.");
        if (body.Length > 2000) body = body[..2000];

        var (post, board, viewer, author, latest) = await _wall.LoadPostContextAsync(postId, UserId);
        if (post is null || board is null || viewer is null || author is null) return NotFound();
        bool staff = _vis.IsStaff(viewer.Role);
        if (!_wall.CanViewPost(board, UserId, staff, author, latest)) return Forbid();

        var c = new PostComment { PostId = postId, UserId = UserId, Body = body };
        _db.PostComments.Add(c);
        await _db.SaveChangesAsync();
        await _notifier.WallChangedAsync(post.BoardId);

        var me = await _db.Users.FindAsync(UserId);
        return new CommentDto(c.Id, UserId, me?.DisplayName ?? "user", c.Body, c.CreatedAt, true);
    }

    [HttpDelete("api/posts/{postId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(int postId, int commentId)
    {
        var c = await _db.PostComments.Include(x => x.Post)
            .FirstOrDefaultAsync(x => x.Id == commentId && x.PostId == postId);
        if (c is null) return NotFound();

        bool isBoardOwner = await _db.Boards.AnyAsync(b => b.Id == c.Post!.BoardId && b.OwnerId == UserId);
        if (c.UserId != UserId && !isBoardOwner) return Forbid();

        _db.PostComments.Remove(c);
        await _db.SaveChangesAsync();
        await _notifier.WallChangedAsync(c.Post!.BoardId);
        return NoContent();
    }
}

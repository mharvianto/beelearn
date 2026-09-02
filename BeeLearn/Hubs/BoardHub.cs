using System.Security.Claims;
using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Hubs;

[Authorize]
public class BoardHub : Hub
{
    private readonly AppDbContext _db;
    private readonly PresenceTracker _presence;
    private readonly DraftStore _drafts;

    public BoardHub(AppDbContext db, PresenceTracker presence, DraftStore drafts)
    {
        _db = db;
        _presence = presence;
        _drafts = drafts;
    }

    private int UserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string BoardGroup(int boardId) => $"board-{boardId}";
    public static string StaffGroup(int boardId) => $"board-{boardId}-staff";

    public async Task JoinBoard(int boardId)
    {
        var membership = await _db.BoardMemberships
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == UserId);
        if (membership is null) throw new HubException("Not a member of this board");

        var name = Context.User!.FindFirstValue(ClaimTypes.Name) ?? "user";
        bool isStaff = membership.Role is MembershipRole.Owner or MembershipRole.Teacher;

        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(boardId));
        if (isStaff) await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup(boardId));

        _presence.Add(Context.ConnectionId, boardId, UserId, name, isStaff);
        await Clients.Group(BoardGroup(boardId)).SendAsync("presence", _presence.ForBoard(boardId));
    }

    public async Task LeaveBoard(int boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(boardId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StaffGroup(boardId));
        _presence.Remove(Context.ConnectionId);
        await Clients.Group(BoardGroup(boardId)).SendAsync("presence", _presence.ForBoard(boardId));
    }

    /// <summary>
    /// Student streams their current editor buffer. Broadcast to staff only so a teacher
    /// can watch progress without the student running or submitting.
    /// </summary>
    public async Task PushDraft(int boardId, int problemId, string code)
    {
        var membership = await _db.BoardMemberships
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == UserId);
        if (membership is null) return;
        if (code is { Length: > 200_000 }) code = code[..200_000];

        var name = Context.User!.FindFirstValue(ClaimTypes.Name) ?? "user";
        var draft = _drafts.Set(boardId, problemId, UserId, name, code ?? "");

        // Staff always see live code; peers see it too unless exam mode / teacher-hidden /
        // the student hid this problem's work. (Staff are in BoardGroup, so in the visible
        // case they just receive the event twice — the client handler is idempotent.)
        await Clients.Group(StaffGroup(boardId)).SendAsync("draftUpdated", draft);
        if (await DraftVisibleToPeersAsync(boardId, problemId, membership))
            await Clients.Group(BoardGroup(boardId)).SendAsync("draftUpdated", draft);
    }

    private async Task<bool> DraftVisibleToPeersAsync(int boardId, int problemId, BoardMembership me)
    {
        var examMode = await _db.Boards.Where(b => b.Id == boardId).Select(b => b.ExamMode).FirstAsync();
        var hiddenByStudent = await _db.Posts
            .Where(p => p.ProblemId == problemId && p.UserId == UserId)
            .Select(p => (bool?)p.HiddenByStudent).FirstOrDefaultAsync() ?? false;
        return Services.WallService.PeerCanSee(examMode, me.HiddenByTeacher, hiddenByStudent);
    }

    /// <summary>Current draft snapshot: full for staff, peer-visible-only for students.</summary>
    public async Task<IEnumerable<Draft>> GetDrafts(int boardId)
    {
        var me = await _db.BoardMemberships
            .Include(m => m.Board)
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == UserId);
        if (me is null) return Enumerable.Empty<Draft>();
        if (me.Role is MembershipRole.Owner or MembershipRole.Teacher)
            return _drafts.ForBoard(boardId);

        if (me.Board!.ExamMode) return Enumerable.Empty<Draft>();
        var hiddenUserIds = await _db.BoardMemberships
            .Where(m => m.BoardId == boardId && m.HiddenByTeacher)
            .Select(m => m.UserId).ToListAsync();
        var hiddenPosts = await _db.Posts
            .Where(p => p.BoardId == boardId && p.HiddenByStudent)
            .Select(p => new { p.ProblemId, p.UserId }).ToListAsync();
        var hiddenPostSet = hiddenPosts.Select(p => (p.ProblemId, p.UserId)).ToHashSet();

        return _drafts.ForBoard(boardId)
            .Where(d => d.UserId != UserId
                        && !hiddenUserIds.Contains(d.UserId)
                        && !hiddenPostSet.Contains((d.ProblemId, d.UserId)));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var boardId = _presence.Remove(Context.ConnectionId);
        if (boardId is int b)
            await Clients.Group(BoardGroup(b)).SendAsync("presence", _presence.ForBoard(b));
        await base.OnDisconnectedAsync(exception);
    }
}

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

    public BoardHub(AppDbContext db, PresenceTracker presence)
    {
        _db = db;
        _presence = presence;
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var boardId = _presence.Remove(Context.ConnectionId);
        if (boardId is int b)
            await Clients.Group(BoardGroup(b)).SendAsync("presence", _presence.ForBoard(b));
        await base.OnDisconnectedAsync(exception);
    }
}

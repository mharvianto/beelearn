using BeeLearn.Hubs;
using BeeLearn.Models;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.SignalR;

namespace BeeLearn.Services;

public class BoardNotifier : IBoardNotifier
{
    private readonly IHubContext<BoardHub> _hub;

    public BoardNotifier(IHubContext<BoardHub> hub) => _hub = hub;

    public Task ProgressChangedAsync(int boardId, int problemId, int authorUserId) =>
        _hub.Clients.Group(BoardHub.BoardGroup(boardId))
            .SendAsync("progressChanged", new { boardId, problemId, userId = authorUserId });

    public Task SubmissionResultAsync(int authorUserId, SubmissionDto submission) =>
        _hub.Clients.User(authorUserId.ToString())
            .SendAsync("submissionResult", submission);

    public Task ProblemChangedAsync(int boardId) =>
        _hub.Clients.Group(BoardHub.BoardGroup(boardId)).SendAsync("problemChanged", new { boardId });

    public Task ExamModeChangedAsync(int boardId, bool examMode) =>
        _hub.Clients.Group(BoardHub.BoardGroup(boardId)).SendAsync("examModeChanged", new { boardId, examMode });

    public Task MemberVisibilityChangedAsync(int boardId, int studentUserId, bool hiddenByTeacher) =>
        _hub.Clients.Group(BoardHub.BoardGroup(boardId))
            .SendAsync("memberVisibilityChanged", new { boardId, studentUserId, hiddenByTeacher });

    public Task WallChangedAsync(int boardId) =>
        _hub.Clients.Group(BoardHub.BoardGroup(boardId)).SendAsync("wallChanged", new { boardId });
}

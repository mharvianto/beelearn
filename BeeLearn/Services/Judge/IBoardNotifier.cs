using BeeLearn.Models;

namespace BeeLearn.Services.Judge;

/// <summary>
/// Push notifications to board clients. Implemented over SignalR; kept as an interface
/// so the judge worker does not depend on the hub directly.
/// </summary>
public interface IBoardNotifier
{
    /// <summary>A submission finished judging: tell the board to refresh its grid.</summary>
    Task ProgressChangedAsync(int boardId, int problemId, int authorUserId);

    /// <summary>Send the author their own (unredacted) submission result.</summary>
    Task SubmissionResultAsync(int authorUserId, SubmissionDto submission);

    /// <summary>Problem added / edited / removed.</summary>
    Task ProblemChangedAsync(int boardId);

    /// <summary>Board exam-mode toggled.</summary>
    Task ExamModeChangedAsync(int boardId, bool examMode);

    /// <summary>Teacher toggled per-student visibility.</summary>
    Task MemberVisibilityChangedAsync(int boardId, int studentUserId, bool hiddenByTeacher);
}

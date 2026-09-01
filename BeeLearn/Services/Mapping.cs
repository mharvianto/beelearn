using BeeLearn.Models;

namespace BeeLearn.Services;

public static class Mapping
{
    public static SubmissionDto ToDto(Submission s, int viewerUserId, bool canSeeCode, string authorName)
    {
        bool mine = s.UserId == viewerUserId;
        return new SubmissionDto(
            s.Id, s.ProblemId, s.UserId, authorName,
            s.Status.ToString(), s.Verdict.ToString(),
            s.RuntimeMs, s.MemoryKb, s.Score,
            s.HiddenByStudent, mine,
            (mine || canSeeCode) ? s.Code : null,
            (mine || canSeeCode) ? s.CompilerOutput : "",
            s.CreatedAt, s.JudgedAt);
    }

    public static TestCaseDto ToDto(TestCase t) =>
        new(t.Id, t.Stdin, t.ExpectedStdout, t.IsSample, t.Points, t.Position);

    public static ProblemDto ToOwnerDto(Problem p) => new(
        p.Id, p.BoardId, p.Title, p.StatementMarkdown, p.Language, p.StarterCode,
        p.TimeLimitMs, p.MemoryLimitKb, p.Position,
        p.TestCases.OrderBy(t => t.Position).ThenBy(t => t.Id).Select(ToDto).ToList());

    public static StudentProblemDto ToStudentDto(Problem p) => new(
        p.Id, p.BoardId, p.Title, p.StatementMarkdown, p.Language, p.StarterCode,
        p.TimeLimitMs, p.MemoryLimitKb, p.Position,
        p.TestCases.Where(t => t.IsSample).OrderBy(t => t.Position).ThenBy(t => t.Id).Select(ToDto).ToList());

    public static ProblemSummaryDto ToSummary(Problem p) =>
        new(p.Id, p.Title, p.Position, p.TimeLimitMs, p.MemoryLimitKb, p.Language);
}

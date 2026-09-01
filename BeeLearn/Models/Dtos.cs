namespace BeeLearn.Models;

// ---- Auth ----
public record RegisterDto(string Email, string Password, string DisplayName, string Role);
public record LoginDto(string Email, string Password);
public record MeDto(int Id, string Email, string DisplayName, string Role);

// ---- Boards ----
public record CreateBoardDto(string Title);
public record JoinBoardDto(string Code);
public record UpdateBoardDto(bool ExamMode);
public record BoardDto(int Id, string Title, string JoinCode, bool ExamMode, bool IsOwner, string Role, int MemberCount, int ProblemCount);

public record MemberDto(int UserId, string DisplayName, string Role, bool HiddenByTeacher);
public record UpdateMemberDto(bool HiddenByTeacher);

// ---- Problems ----
public record TestCaseDto(int Id, string Stdin, string ExpectedStdout, bool IsSample, int Points, int Position);
public record UpsertTestCaseDto(int? Id, string Stdin, string ExpectedStdout, bool IsSample, int Points, int Position);
public record UpsertProblemDto(
    string Title,
    string StatementMarkdown,
    string Language,
    string StarterCode,
    int TimeLimitMs,
    int MemoryLimitKb,
    int Position,
    List<UpsertTestCaseDto> TestCases);

/// <summary>Full problem view for the board owner/teacher.</summary>
public record ProblemDto(
    int Id, int BoardId, string Title, string StatementMarkdown, string Language,
    string StarterCode, int TimeLimitMs, int MemoryLimitKb, int Position,
    List<TestCaseDto> TestCases);

/// <summary>Problem view for a student: only sample tests exposed.</summary>
public record StudentProblemDto(
    int Id, int BoardId, string Title, string StatementMarkdown, string Language,
    string StarterCode, int TimeLimitMs, int MemoryLimitKb, int Position,
    List<TestCaseDto> SampleTests);

// ---- Submissions ----
public record SubmitDto(string Code);
public record UpdateSubmissionDto(bool HiddenByStudent);
public record SubmissionDto(
    int Id, int ProblemId, int UserId, string AuthorName,
    string Status, string Verdict, int RuntimeMs, int MemoryKb, double Score,
    bool HiddenByStudent, bool Mine, string? Code, string CompilerOutput,
    DateTime CreatedAt, DateTime? JudgedAt);

// ---- Live board / progress grid ----
public record ProgressCellDto(
    int UserId, int ProblemId,
    string Verdict,        // "None" if never attempted
    double Score,
    int Attempts,
    bool Redacted,         // true => viewer may only see that an attempt exists
    bool Latest);          // latest submission is Accepted

public record ProgressBoardDto(
    int BoardId,
    bool ExamMode,
    bool ViewerIsStaff,
    List<MemberDto> Students,
    List<ProblemSummaryDto> Problems,
    List<ProgressCellDto> Cells);

public record ProblemSummaryDto(int Id, string Title, int Position, int TimeLimitMs, int MemoryLimitKb, string Language);

// ---- Ad-hoc run ----
public record RunDto(string Language, string Code, string Stdin);
public record RunResultDto(
    bool CompileOk, string CompilerOutput,
    string Stdout, string Stderr,
    int RuntimeMs, int MemoryKb,
    bool TimedOut, int ExitCode, int Signal);

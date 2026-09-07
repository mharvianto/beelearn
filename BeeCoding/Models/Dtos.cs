namespace BeeCoding.Models;

// ---- Auth ----
public record RegisterDto(string Email, string Password, string DisplayName, string Role, string? TeacherCode = null);
public record LoginDto(string Email, string Password);
public record MeDto(int Id, string Email, string DisplayName, string Role);

// ---- Boards ----
public record CreateBoardDto(string Title);
public record JoinBoardDto(string Code);
public record UpdateBoardDto(bool? ExamMode, bool? ProtectContent);
public record BoardDto(int Id, string Slug, string Title, string JoinCode, bool ExamMode, bool ProtectContent, bool IsOwner, string Role, int MemberCount, int ProblemCount);

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
    string? Tags,
    string? Level,
    List<UpsertTestCaseDto>? TestCases);   // null => leave test cases untouched

/// <summary>Full problem view for the board owner/teacher.</summary>
public record ProblemDto(
    int Id, int BoardId, string Title, string StatementMarkdown, string Language,
    string StarterCode, int TimeLimitMs, int MemoryLimitKb, int Position,
    string Tags, string Level,
    List<TestCaseDto> TestCases);

/// <summary>Problem view for a student: only sample tests exposed.</summary>
public record StudentProblemDto(
    int Id, int BoardId, string Title, string StatementMarkdown, string Language,
    string StarterCode, int TimeLimitMs, int MemoryLimitKb, int Position,
    string Tags, string Level,
    List<TestCaseDto> SampleTests);

// ---- Problem bank ----
public record BankSummaryDto(
    int Id, string Title, string Language, string Level, string Tags, bool IsPublic,
    bool Mine, string OwnerName, int TestCount, int SampleCount, DateTime UpdatedAt);

public record BankProblemDto(
    int Id, string Title, string StatementMarkdown, string Language, string StarterCode,
    int TimeLimitMs, int MemoryLimitKb, string Level, string Tags, bool IsPublic,
    bool Mine, string OwnerName, DateTime UpdatedAt,
    List<TestCaseDto> TestCases);   // full set only for the owner; samples only otherwise

public record UpsertBankProblemDto(
    string Title, string StatementMarkdown, string Language, string StarterCode,
    int TimeLimitMs, int MemoryLimitKb, string? Level, string Tags, bool IsPublic,
    List<UpsertTestCaseDto>? TestCases);   // null => leave test cases untouched

// ---- Admin ingest (token-authed, for scripting the problem bank) ----
public record AdminTestInput(string Stdin, string ExpectedStdout, bool? IsSample, int? Points, int? Position);
public record AdminBankProblemInput(
    string Title,
    string StatementMarkdown,
    string? Language,          // "c" | "cpp"  (default cpp)
    string? Level,             // Easy | Medium | Hard  (default Medium)
    string? Tags,
    string? StarterCode,
    int? TimeLimitMs,
    int? MemoryLimitKb,
    bool? IsPublic,            // default true
    List<AdminTestInput>? Tests);
public record AdminIngestDto(
    string? OwnerEmail,        // an existing Teacher; default = first teacher
    bool? ReplaceExisting,     // default true: upsert by (owner, title)
    List<AdminBankProblemInput> Problems);
public record AdminBankRow(int Id, string Title, string Language, string Level, string Tags,
    bool IsPublic, int TestCount, int SampleCount, DateTime UpdatedAt);
public record AdminIngestResultDto(
    List<AdminBankRow> Created, List<AdminBankRow> Updated, List<string> Errors);

// ---- Practice (students solve bank problems) ----
public record PracticeSummaryDto(
    int Id, string Title, string Language, string Level, string Tags,
    string MyVerdict, double MyBestScore, bool Solved);

public record PracticePageDto(
    int Total, int Solved, int Page, int PageSize, List<PracticeSummaryDto> Items);

public record PracticeProblemDto(
    int Id, string Title, string StatementMarkdown, string Language, string StarterCode,
    int TimeLimitMs, int MemoryLimitKb, string Level, string Tags,
    List<TestCaseDto> SampleTests, bool Solved);

public record BankSubmissionDto(
    int Id, int BankProblemId, string Status, string Verdict,
    int RuntimeMs, int MemoryKb, double Score, string CompilerOutput,
    DateTime CreatedAt, DateTime? JudgedAt, string? Code);

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
    bool Latest,           // latest submission is Accepted
    DateTime LastAt);      // time of the latest submission

public record ProgressBoardDto(
    int BoardId,
    bool ExamMode,
    bool ViewerIsStaff,
    List<MemberDto> Students,
    List<ProblemSummaryDto> Problems,
    List<ProgressCellDto> Cells);

public record ProblemSummaryDto(int Id, string Title, int Position, int TimeLimitMs, int MemoryLimitKb, string Language, string Tags, string Level);

// ---- Padlet-style wall ----
public record ReactionDto(string Emoji, int Count, bool Mine);
public record CommentDto(int Id, int UserId, string AuthorName, string Body, DateTime CreatedAt, bool CanDelete);

public record WallPostDto(
    int PostId,
    int ProblemId,
    int UserId,
    string AuthorName,
    string Note,
    bool Mine,
    bool Redacted,            // peer post hidden -> show only that it exists
    bool HiddenByStudent,     // author's own "hide from peers" state (for the toggle)
    string Verdict,           // "None" if not yet judged / no submission
    double Score,
    int Attempts,
    int RuntimeMs,
    int MemoryKb,
    string Language,
    string? CodePreview,      // first lines of latest submission, null when not visible
    DateTime UpdatedAt,
    List<ReactionDto> Reactions,
    List<CommentDto> Comments);

public record WallDto(
    int BoardId,
    bool ExamMode,
    bool ViewerIsStaff,
    List<ProblemSummaryDto> Problems,
    List<WallPostDto> Posts);

public record NoteDto(string Note);
public record ReactDto(string Emoji);
public record CommentBodyDto(string Body);

// ---- Ad-hoc run ----
public record RunDto(string Language, string Code, string Stdin);
public record RunResultDto(
    bool CompileOk, string CompilerOutput,
    string Stdout, string Stderr,
    int RuntimeMs, int MemoryKb,
    bool TimedOut, int ExitCode, int Signal);

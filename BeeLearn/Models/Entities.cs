using System.ComponentModel.DataAnnotations;

namespace BeeLearn.Models;

public enum UserRole { Teacher, Student }

public enum ProblemLevel { Easy = 1, Medium = 2, Hard = 3 }

public enum MembershipRole { Owner, Teacher, Student }

public enum SubmissionStatus { Queued, Running, Done }

public enum Verdict
{
    None,
    Accepted,
    WrongAnswer,
    TimeLimit,
    MemoryLimit,
    RuntimeError,
    CompileError
}

public class User
{
    public int Id { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    [MaxLength(120)]
    public string DisplayName { get; set; } = "";

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BoardMembership> Memberships { get; set; } = new();
}

public class Board
{
    public int Id { get; set; }

    [MaxLength(160)]
    public string Title { get; set; } = "";

    /// <summary>Human-typed code to join the board (short, rotatable).</summary>
    [MaxLength(12)]
    public string JoinCode { get; set; } = "";

    /// <summary>Unguessable public identifier used in URLs (the int Id stays internal).</summary>
    [MaxLength(24)]
    public string Slug { get; set; } = "";

    public int OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Board-wide exam mode: students never see peers' answers/progress.</summary>
    public bool ExamMode { get; set; }

    /// <summary>Deter casual copying/screenshots of problem statements (select/copy blocked,
    /// blur-on-leave, name watermark). Cannot truly stop a camera — makes leaks attributable.</summary>
    public bool ProtectContent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BoardMembership> Members { get; set; } = new();
    public List<Problem> Problems { get; set; } = new();
}

public class BoardMembership
{
    public int Id { get; set; }

    public int BoardId { get; set; }
    public Board? Board { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public MembershipRole Role { get; set; }

    /// <summary>Feature 5 (per-student): teacher hides this student's cells from other students.</summary>
    public bool HiddenByTeacher { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class Problem
{
    public int Id { get; set; }

    public int BoardId { get; set; }
    public Board? Board { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public string StatementMarkdown { get; set; } = "";

    /// <summary>"c" or "cpp".</summary>
    [MaxLength(8)]
    public string Language { get; set; } = "cpp";

    /// <summary>Comma-separated, lowercase topic tags, e.g. "array,graph,dp".</summary>
    [MaxLength(300)]
    public string Tags { get; set; } = "";

    public ProblemLevel Level { get; set; } = ProblemLevel.Medium;

    public string StarterCode { get; set; } = "";

    public int TimeLimitMs { get; set; } = 1000;

    public int MemoryLimitKb { get; set; } = 32768;

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Provenance when this problem was copied in from the bank.</summary>
    public int? SourceBankProblemId { get; set; }

    public List<TestCase> TestCases { get; set; } = new();
}

/// <summary>
/// A reusable problem in a teacher's private bank. Adding one to a board COPIES it into
/// <see cref="Problem"/> — the board copy is independent afterwards.
/// </summary>
public class BankProblem
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public User? Owner { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public string StatementMarkdown { get; set; } = "";

    [MaxLength(8)]
    public string Language { get; set; } = "cpp";

    public string StarterCode { get; set; } = "";

    public int TimeLimitMs { get; set; } = 1000;

    public int MemoryLimitKb { get; set; } = 32_768;

    public ProblemLevel Level { get; set; } = ProblemLevel.Medium;

    /// <summary>Comma-separated, lowercase topic tags.</summary>
    [MaxLength(300)]
    public string Tags { get; set; } = "";

    /// <summary>Other teachers may browse and copy it (hidden tests are never sent to them).</summary>
    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<BankTestCase> TestCases { get; set; } = new();
}

public class BankTestCase
{
    public int Id { get; set; }

    public int BankProblemId { get; set; }
    public BankProblem? BankProblem { get; set; }

    public string Stdin { get; set; } = "";
    public string ExpectedStdout { get; set; } = "";
    public bool IsSample { get; set; }
    public int Points { get; set; } = 1;
    public int Position { get; set; }
}

public class TestCase
{
    public int Id { get; set; }

    public int ProblemId { get; set; }
    public Problem? Problem { get; set; }

    public string Stdin { get; set; } = "";

    public string ExpectedStdout { get; set; } = "";

    public bool IsSample { get; set; }

    public int Points { get; set; } = 1;

    public int Position { get; set; }
}

public class Submission
{
    public int Id { get; set; }

    public int ProblemId { get; set; }
    public Problem? Problem { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Code { get; set; } = "";

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Queued;

    public Verdict Verdict { get; set; } = Verdict.None;

    public int RuntimeMs { get; set; }

    public int MemoryKb { get; set; }

    /// <summary>0..1 fraction of testcase points passed.</summary>
    public double Score { get; set; }

    /// <summary>Feature 4: student hides their own answer from other students.</summary>
    public bool HiddenByStudent { get; set; }

    public string CompilerOutput { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? JudgedAt { get; set; }
}

/// <summary>
/// A student's living "post" on the board wall for one problem. Persists across
/// resubmissions (always shows the latest submission) and carries the social bits.
/// </summary>
public class Post
{
    public int Id { get; set; }

    public int BoardId { get; set; }
    public Board? Board { get; set; }

    public int ProblemId { get; set; }
    public Problem? Problem { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Free-text caption the author can add ("stuck on test 3").</summary>
    public string Note { get; set; } = "";

    /// <summary>Student hides this problem's work (live draft + submitted card) from peers.</summary>
    public bool HiddenByStudent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PostReaction> Reactions { get; set; } = new();
    public List<PostComment> Comments { get; set; } = new();
}

public class PostReaction
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(16)]
    public string Emoji { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostComment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(2000)]
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

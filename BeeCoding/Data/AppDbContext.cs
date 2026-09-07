using BeeCoding.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMembership> BoardMemberships => Set<BoardMembership>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<BankProblem> BankProblems => Set<BankProblem>();
    public DbSet<BankTestCase> BankTestCases => Set<BankTestCase>();
    public DbSet<BankSubmission> BankSubmissions => Set<BankSubmission>();
    public DbSet<SolveRecord> SolveRecords => Set<SolveRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<Board>().HasIndex(x => x.JoinCode).IsUnique();
        b.Entity<Board>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Board>()
            .HasOne(x => x.Owner).WithMany()
            .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<BoardMembership>().HasIndex(x => new { x.BoardId, x.UserId }).IsUnique();
        b.Entity<BoardMembership>()
            .HasOne(x => x.Board).WithMany(x => x.Members)
            .HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BoardMembership>()
            .HasOne(x => x.User).WithMany(x => x.Memberships)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Problem>()
            .HasOne(x => x.Board).WithMany(x => x.Problems)
            .HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TestCase>()
            .HasOne(x => x.Problem).WithMany(x => x.TestCases)
            .HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Submission>()
            .HasOne(x => x.Problem).WithMany()
            .HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Submission>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Submission>().HasIndex(x => new { x.ProblemId, x.UserId });

        b.Entity<Post>().HasIndex(x => new { x.ProblemId, x.UserId }).IsUnique();
        b.Entity<Post>().HasIndex(x => x.BoardId);
        b.Entity<Post>()
            .HasOne(x => x.Board).WithMany()
            .HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Post>()
            .HasOne(x => x.Problem).WithMany()
            .HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Post>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<PostReaction>().HasIndex(x => new { x.PostId, x.UserId, x.Emoji }).IsUnique();
        b.Entity<PostReaction>()
            .HasOne(x => x.Post).WithMany(p => p.Reactions)
            .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PostReaction>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<BankProblem>().HasIndex(x => x.OwnerId);
        b.Entity<BankProblem>().HasIndex(x => x.IsPublic);
        b.Entity<BankProblem>()
            .HasOne(x => x.Owner).WithMany()
            .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BankTestCase>()
            .HasOne(x => x.BankProblem).WithMany(p => p.TestCases)
            .HasForeignKey(x => x.BankProblemId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<BankSubmission>().HasIndex(x => new { x.BankProblemId, x.UserId });
        b.Entity<BankSubmission>()
            .HasOne(x => x.BankProblem).WithMany()
            .HasForeignKey(x => x.BankProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BankSubmission>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<SolveRecord>().HasIndex(x => new { x.UserId, x.ProblemKey }).IsUnique();
        b.Entity<SolveRecord>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<PostComment>().HasIndex(x => x.PostId);
        b.Entity<PostComment>()
            .HasOne(x => x.Post).WithMany(p => p.Comments)
            .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PostComment>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

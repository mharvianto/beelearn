using BeeLearn.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMembership> BoardMemberships => Set<BoardMembership>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<Board>().HasIndex(x => x.JoinCode).IsUnique();
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
    }
}

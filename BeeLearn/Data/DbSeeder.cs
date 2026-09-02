using BeeLearn.Models;
using BeeLearn.Services;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, PasswordService pw)
    {
        if (await db.Users.AnyAsync()) return;

        var teacher = new User
        {
            Email = "teacher@demo.test",
            DisplayName = "Demo Teacher",
            Role = UserRole.Teacher,
        };
        teacher.PasswordHash = pw.Hash(teacher, "password");
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var board = new Board { Title = "Demo Board", OwnerId = teacher.Id, JoinCode = "DEMO01", Slug = "demo-board" };
        db.Boards.Add(board);
        db.BoardMemberships.Add(new BoardMembership
        {
            Board = board, UserId = teacher.Id, Role = MembershipRole.Owner,
        });
        await db.SaveChangesAsync();

        var problem = new Problem
        {
            BoardId = board.Id,
            Title = "A + B",
            StatementMarkdown =
                "Read two integers `a` and `b` on one line, print `a + b`.\n\n" +
                "**Input:** `2 3` — **Output:** `5`",
            Language = "cpp",
            StarterCode =
                "#include <iostream>\nusing namespace std;\nint main(){\n    long long a, b;\n    cin >> a >> b;\n    cout << a + b << endl;\n}\n",
            TimeLimitMs = 1000,
            MemoryLimitKb = 32_768,
            Position = 0,
        };
        problem.TestCases.Add(new TestCase { Stdin = "2 3\n", ExpectedStdout = "5\n", IsSample = true, Points = 0, Position = 0 });
        problem.TestCases.Add(new TestCase { Stdin = "100 -40\n", ExpectedStdout = "60\n", IsSample = false, Points = 1, Position = 1 });
        problem.TestCases.Add(new TestCase { Stdin = "1000000000 1000000000\n", ExpectedStdout = "2000000000\n", IsSample = false, Points = 1, Position = 2 });
        db.Problems.Add(problem);
        await db.SaveChangesAsync();
    }
}

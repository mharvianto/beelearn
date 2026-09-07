using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeLearn.Migrations
{
    /// <inheritdoc />
    public partial class PracticeAndXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Xp",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BankSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankProblemId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Verdict = table.Column<int>(type: "INTEGER", nullable: false),
                    RuntimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryKb = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    CompilerOutput = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    JudgedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankSubmissions_BankProblems_BankProblemId",
                        column: x => x.BankProblemId,
                        principalTable: "BankProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankSubmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolveRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProblemKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    XpAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolveRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolveRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankSubmissions_BankProblemId_UserId",
                table: "BankSubmissions",
                columns: new[] { "BankProblemId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankSubmissions_UserId",
                table: "BankSubmissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SolveRecords_UserId_ProblemKey",
                table: "SolveRecords",
                columns: new[] { "UserId", "ProblemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankSubmissions");

            migrationBuilder.DropTable(
                name: "SolveRecords");

            migrationBuilder.DropColumn(
                name: "Xp",
                table: "Users");
        }
    }
}

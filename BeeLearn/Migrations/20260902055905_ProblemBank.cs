using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeLearn.Migrations
{
    /// <inheritdoc />
    public partial class ProblemBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceBankProblemId",
                table: "Problems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankProblems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StatementMarkdown = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StarterCode = table.Column<string>(type: "TEXT", nullable: false),
                    TimeLimitMs = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryLimitKb = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankProblems_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankTestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankProblemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stdin = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedStdout = table.Column<string>(type: "TEXT", nullable: false),
                    IsSample = table.Column<bool>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTestCases_BankProblems_BankProblemId",
                        column: x => x.BankProblemId,
                        principalTable: "BankProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankProblems_IsPublic",
                table: "BankProblems",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_BankProblems_OwnerId",
                table: "BankProblems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTestCases_BankProblemId",
                table: "BankTestCases",
                column: "BankProblemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTestCases");

            migrationBuilder.DropTable(
                name: "BankProblems");

            migrationBuilder.DropColumn(
                name: "SourceBankProblemId",
                table: "Problems");
        }
    }
}

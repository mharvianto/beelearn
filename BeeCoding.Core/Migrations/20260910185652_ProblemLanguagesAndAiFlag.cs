using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeCoding.Migrations
{
    /// <inheritdoc />
    public partial class ProblemLanguagesAndAiFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "StarterCode",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "BankProblems");

            migrationBuilder.DropColumn(
                name: "StarterCode",
                table: "BankProblems");

            migrationBuilder.AddColumn<string>(
                name: "AllowedLanguages",
                table: "Problems",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedByAi",
                table: "Problems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedLanguages",
                table: "BankProblems",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedByAi",
                table: "BankProblems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedLanguages",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "GeneratedByAi",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "AllowedLanguages",
                table: "BankProblems");

            migrationBuilder.DropColumn(
                name: "GeneratedByAi",
                table: "BankProblems");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Problems",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StarterCode",
                table: "Problems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "BankProblems",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StarterCode",
                table: "BankProblems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}

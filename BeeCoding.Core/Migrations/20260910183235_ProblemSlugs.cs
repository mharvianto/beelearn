using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeCoding.Migrations
{
    /// <inheritdoc />
    public partial class ProblemSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Problems",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "BankProblems",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // Backfill rows that predate slugs with a random, distinct value so the
            // unique index below can be created. New rows get a proper slug from
            // AppDbContext.SaveChangesAsync; these legacy ones just need to be unique.
            migrationBuilder.Sql("UPDATE \"Problems\" SET \"Slug\" = lower(hex(randomblob(6))) WHERE \"Slug\" = '';");
            migrationBuilder.Sql("UPDATE \"BankProblems\" SET \"Slug\" = lower(hex(randomblob(6))) WHERE \"Slug\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Slug",
                table: "Problems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankProblems_Slug",
                table: "BankProblems",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Problems_Slug",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_BankProblems_Slug",
                table: "BankProblems");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "BankProblems");
        }
    }
}

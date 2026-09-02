using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeLearn.Migrations
{
    /// <inheritdoc />
    public partial class BoardSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Boards",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            // Backfill unique slugs for existing rows before the unique index is created
            // (app startup also backfills, but the index must not see duplicate "").
            migrationBuilder.Sql(
                "UPDATE \"Boards\" SET \"Slug\" = lower(hex(randomblob(12))) WHERE \"Slug\" IS NULL OR \"Slug\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_Slug",
                table: "Boards",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Boards_Slug",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Boards");
        }
    }
}

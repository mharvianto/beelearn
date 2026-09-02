using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeLearn.Migrations
{
    /// <inheritdoc />
    public partial class PostHiddenByStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HiddenByStudent",
                table: "Posts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenByStudent",
                table: "Posts");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeCoding.Migrations
{
    /// <inheritdoc />
    public partial class ProblemTagsNoCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Problems",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            // Fold any existing Category text into Tags before dropping the column.
            migrationBuilder.Sql(
                "UPDATE \"Problems\" SET \"Tags\" = lower(\"Category\") " +
                "WHERE \"Category\" IS NOT NULL AND \"Category\" <> '';");
            migrationBuilder.Sql(
                "UPDATE \"BankProblems\" SET \"Tags\" = CASE WHEN \"Tags\" = '' THEN lower(\"Category\") " +
                "ELSE \"Tags\" || ',' || lower(\"Category\") END " +
                "WHERE \"Category\" IS NOT NULL AND \"Category\" <> '';");

            migrationBuilder.DropColumn(name: "Category", table: "Problems");
            migrationBuilder.DropColumn(name: "Category", table: "BankProblems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Problems");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Problems",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BankProblems",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");
        }
    }
}

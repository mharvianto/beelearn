using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeCoding.Migrations
{
    /// <inheritdoc />
    public partial class AiProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GenerateModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProviderConfigs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderConfigs_OrganizationId",
                table: "AiProviderConfigs",
                column: "OrganizationId",
                unique: true,
                filter: "OrganizationId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderConfigs");
        }
    }
}

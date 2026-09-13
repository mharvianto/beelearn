using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeeCoding.Migrations
{
    /// <inheritdoc />
    public partial class LtiSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LtiPlatforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeploymentIds = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AuthLoginUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AuthTokenUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JwksUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiPlatforms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LtiToolKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PrivateKeyPem = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiToolKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LtiResourceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LtiPlatformId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeploymentId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContextId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ResourceLinkId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: true),
                    LineItemUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiResourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LtiResourceLinks_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LtiResourceLinks_LtiPlatforms_LtiPlatformId",
                        column: x => x.LtiPlatformId,
                        principalTable: "LtiPlatforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LtiUserLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LtiPlatformId = table.Column<int>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiUserLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LtiUserLinks_LtiPlatforms_LtiPlatformId",
                        column: x => x.LtiPlatformId,
                        principalTable: "LtiPlatforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LtiUserLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LtiPlatforms_Issuer_ClientId",
                table: "LtiPlatforms",
                columns: new[] { "Issuer", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiResourceLinks_BoardId",
                table: "LtiResourceLinks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_LtiResourceLinks_LtiPlatformId_DeploymentId_ContextId_ResourceLinkId",
                table: "LtiResourceLinks",
                columns: new[] { "LtiPlatformId", "DeploymentId", "ContextId", "ResourceLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiUserLinks_LtiPlatformId_Subject",
                table: "LtiUserLinks",
                columns: new[] { "LtiPlatformId", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiUserLinks_UserId",
                table: "LtiUserLinks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LtiResourceLinks");

            migrationBuilder.DropTable(
                name: "LtiToolKeys");

            migrationBuilder.DropTable(
                name: "LtiUserLinks");

            migrationBuilder.DropTable(
                name: "LtiPlatforms");
        }
    }
}

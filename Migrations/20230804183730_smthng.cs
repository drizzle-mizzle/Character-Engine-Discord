using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class Smthng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaiHistories");

            migrationBuilder.DropColumn(
                name: "ReplyDelay",
                table: "CharacterWebhooks");

            migrationBuilder.AddColumn<string>(
                name: "GuildJailbreakPrompt",
                table: "Guilds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCallTime",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildJailbreakPrompt",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "LastCallTime",
                table: "CharacterWebhooks");

            migrationBuilder.AddColumn<int>(
                name: "ReplyDelay",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CaiHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterWebhookId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaiHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaiHistories_CharacterWebhooks_CharacterWebhookId",
                        column: x => x.CharacterWebhookId,
                        principalTable: "CharacterWebhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaiHistories_CharacterWebhookId",
                table: "CaiHistories",
                column: "CharacterWebhookId");
        }
    }
}

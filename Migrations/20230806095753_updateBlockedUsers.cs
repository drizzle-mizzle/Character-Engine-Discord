using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class updateBlockedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "From",
                table: "BlockedUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<ulong>(
                name: "GuildId",
                table: "BlockedUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hours",
                table: "BlockedUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BlockedUsers_GuildId",
                table: "BlockedUsers",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_Guilds_GuildId",
                table: "BlockedUsers",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_Guilds_GuildId",
                table: "BlockedUsers");

            migrationBuilder.DropIndex(
                name: "IX_BlockedUsers_GuildId",
                table: "BlockedUsers");

            migrationBuilder.DropColumn(
                name: "From",
                table: "BlockedUsers");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "BlockedUsers");

            migrationBuilder.DropColumn(
                name: "Hours",
                table: "BlockedUsers");
        }
    }
}

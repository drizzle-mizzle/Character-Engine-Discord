using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ChatHistory_PK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ChatHistories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ChatHistories");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories",
                columns: new[] { "SpawnedCharacterId", "CreatedAt" });
        }
    }
}

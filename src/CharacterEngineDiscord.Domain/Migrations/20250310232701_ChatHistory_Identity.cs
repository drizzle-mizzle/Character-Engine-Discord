using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ChatHistory_Identity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ChatHistories");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "ChatHistories",
                type: "bigint",
                nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories",
                columns: new[] { "Id", "SpawnedCharacterId" });
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

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ChatHistories",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatHistories",
                table: "ChatHistories",
                column: "Id");
        }
    }
}

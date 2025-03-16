using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ChatHistory_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatHistories_SpawnedCharacterId",
                table: "ChatHistories");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_Id_SpawnedCharacterId",
                table: "ChatHistories",
                columns: new[] { "Id", "SpawnedCharacterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatHistories_Id_SpawnedCharacterId",
                table: "ChatHistories");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_SpawnedCharacterId",
                table: "ChatHistories",
                column: "SpawnedCharacterId");
        }
    }
}

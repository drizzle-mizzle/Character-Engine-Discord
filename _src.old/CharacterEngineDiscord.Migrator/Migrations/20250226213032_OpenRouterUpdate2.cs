using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class OpenRouterUpdate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdoptedCharacterAuthorLink",
                table: "OpenRouterSpawnedCharacters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdoptedCharacterAuthorLink",
                table: "OpenRouterSpawnedCharacters");
        }
    }
}

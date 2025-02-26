using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class OpenRouterUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardCharacterDescription",
                table: "OpenRouterSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "CharacterStat",
                table: "OpenRouterSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "CaiDefinition",
                table: "CaiSpawnedCharacters");

            migrationBuilder.RenameColumn(
                name: "CharacterSourceType",
                table: "OpenRouterSpawnedCharacters",
                newName: "AdoptedCharacterSourceType");

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "SakuraAiSpawnedCharacters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SakuraExampleDialog",
                table: "SakuraAiSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "OpenRouterSpawnedCharacters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdoptedCharacterDefinition",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdoptedCharacterLink",
                table: "OpenRouterSpawnedCharacters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "CaiSpawnedCharacters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaiTitle",
                table: "CaiSpawnedCharacters",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CaiChatId",
                table: "CaiSpawnedCharacters",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SakuraExampleDialog",
                table: "SakuraAiSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "AdoptedCharacterDefinition",
                table: "OpenRouterSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "AdoptedCharacterLink",
                table: "OpenRouterSpawnedCharacters");

            migrationBuilder.RenameColumn(
                name: "AdoptedCharacterSourceType",
                table: "OpenRouterSpawnedCharacters",
                newName: "CharacterSourceType");

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "SakuraAiSpawnedCharacters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardCharacterDescription",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CharacterStat",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "CharacterImageLink",
                table: "CaiSpawnedCharacters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaiTitle",
                table: "CaiSpawnedCharacters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3000)",
                oldMaxLength: 3000);

            migrationBuilder.AlterColumn<string>(
                name: "CaiChatId",
                table: "CaiSpawnedCharacters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaiDefinition",
                table: "CaiSpawnedCharacters",
                type: "text",
                nullable: true);
        }
    }
}

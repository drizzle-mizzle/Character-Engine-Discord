using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class Rename1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalApiToken",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "MessagesFormat",
                table: "CharacterWebhooks",
                newName: "PersonalMessagesFormat");

            migrationBuilder.RenameColumn(
                name: "PersonalOpenAiApiEndpoint",
                table: "CharacterWebhooks",
                newName: "PersonalApiEndpoint");

            migrationBuilder.RenameColumn(
                name: "OpenAiModel",
                table: "CharacterWebhooks",
                newName: "PersonalApiModel");

            migrationBuilder.RenameColumn(
                name: "UniversalJailbreakPrompt",
                table: "CharacterWebhooks",
                newName: "PersonalJailbreakPrompt");

            migrationBuilder.RenameColumn(
                name: "OpenAiFreqPenalty",
                table: "CharacterWebhooks",
                newName: "GenerationFreqPenalty");

            migrationBuilder.RenameColumn(
                name: "OpenAiPresencePenalty",
                table: "CharacterWebhooks",
                newName: "GenerationPresencePenalty");

            migrationBuilder.RenameColumn(
                name: "OpenAiTemperature",
                table: "CharacterWebhooks",
                newName: "GenerationTemperature");

            migrationBuilder.RenameColumn(
                name: "OpenAiMaxTokens",
                table: "CharacterWebhooks",
                newName: "GenerationMaxTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonalApiToken",
                table: "CharacterWebhooks");

            migrationBuilder.RenameColumn(
                name: "PersonalMessagesFormat",
                table: "CharacterWebhooks",
                newName: "MessagesFormat");

            migrationBuilder.RenameColumn(
                name: "PersonalApiEndpoint",
                table: "CharacterWebhooks",
                newName: "PersonalOpenAiApiEndpoint");

            migrationBuilder.RenameColumn(
                name: "PersonalApiModel",
                table: "CharacterWebhooks",
                newName: "OpenAiModel");

            migrationBuilder.RenameColumn(
                name: "PersonalJailbreakPrompt",
                table: "CharacterWebhooks",
                newName: "UniversalJailbreakPrompt");

            migrationBuilder.RenameColumn(
                name: "GenerationFreqPenalty",
                table: "CharacterWebhooks",
                newName: "OpenAiFreqPenalty");

            migrationBuilder.RenameColumn(
                name: "GenerationPresencePenalty",
                table: "CharacterWebhooks",
                newName: "OpenAiPresencePenalty");

            migrationBuilder.RenameColumn(
                name: "GenerationTemperature",
                table: "CharacterWebhooks",
                newName: "OpenAiTemperature");

            migrationBuilder.RenameColumn(
                name: "GenerationMaxTokens",
                table: "CharacterWebhooks",
                newName: "OpenAiMaxTokens");
        }
    }
}

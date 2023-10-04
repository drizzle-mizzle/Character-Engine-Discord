using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class Upd2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenAiHistoryMessages_CharacterWebhooks_CharacterWebhookId",
                table: "StoredHistoryMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OpenAiHistoryMessages",
                table: "StoredHistoryMessages");

            migrationBuilder.RenameColumn(
                name: "LastUserMsgUuId",
                table: "CharacterWebhooks",
                newName: "LastUserMsgId");

            migrationBuilder.RenameColumn(
                name: "LastCharacterMsgUuId",
                table: "CharacterWebhooks",
                newName: "LastCharacterMsgId");

            migrationBuilder.RenameColumn(
                name: "GenerationPresencePenalty",
                table: "CharacterWebhooks",
                newName: "GenerationPresenceOrRepetitionPenalty");

            migrationBuilder.RenameColumn(
                name: "GenerationFreqPenalty",
                table: "CharacterWebhooks",
                newName: "GenerationFreqPenaltyOrRepetitionSlope");

            migrationBuilder.RenameColumn(
                name: "CaiActiveHistoryId",
                table: "CharacterWebhooks",
                newName: "ActiveHistoryID");

            migrationBuilder.RenameIndex(
                name: "IX_OpenAiHistoryMessages_CharacterWebhookId",
                table: "StoredHistoryMessages",
                newName: "IX_StoredHistoryMessages_CharacterWebhookId");

            migrationBuilder.AddColumn<float>(
                name: "GenerationTypicalSampling",
                table: "CharacterWebhooks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuildHordeModel",
                table: "Guilds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationContextSizeTokens",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: true);


            migrationBuilder.AddColumn<float>(
                name: "GenerationTailfreeSampling",
                table: "CharacterWebhooks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "GenerationTopP",
                table: "CharacterWebhooks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "GenerationTopA",
                table: "CharacterWebhooks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationTopK",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StoredHistoryMessages",
                table: "StoredHistoryMessages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredHistoryMessages_CharacterWebhooks_CharacterWebhookId",
                table: "StoredHistoryMessages",
                column: "CharacterWebhookId",
                principalTable: "CharacterWebhooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredHistoryMessages_CharacterWebhooks_CharacterWebhookId",
                table: "StoredHistoryMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StoredHistoryMessages",
                table: "StoredHistoryMessages");

            migrationBuilder.DropColumn(
                name: "GuildHordeModel",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "GenerationContextSizeTokens",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "GenerationTopP",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "GenerationTypicalSampling",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "GenerationTailfreeSampling",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "GenerationTopA",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "GenerationTopK",
                table: "CharacterWebhooks");

            migrationBuilder.RenameColumn(
                name: "LastUserMsgId",
                table: "CharacterWebhooks",
                newName: "LastUserMsgUuId");

            migrationBuilder.RenameColumn(
                name: "LastCharacterMsgId",
                table: "CharacterWebhooks",
                newName: "LastCharacterMsgUuId");

            migrationBuilder.RenameColumn(
                name: "GenerationPresenceOrRepetitionPenalty",
                table: "CharacterWebhooks",
                newName: "GenerationPresencePenalty");

            migrationBuilder.RenameColumn(
                name: "GenerationFreqPenaltyOrRepetitionSlope",
                table: "CharacterWebhooks",
                newName: "GenerationFreqPenalty");

            migrationBuilder.RenameColumn(
                name: "ActiveHistoryID",
                table: "CharacterWebhooks",
                newName: "CaiActiveHistoryId");

            migrationBuilder.RenameIndex(
                name: "IX_StoredHistoryMessages_CharacterWebhookId",
                table: "StoredHistoryMessages",
                newName: "IX_StoredHistoryMessages_CharacterWebhookId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StoredHistoryMessages",
                table: "StoredHistoryMessages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredHistoryMessages_CharacterWebhooks_CharacterWebhookId",
                table: "StoredHistoryMessages",
                column: "CharacterWebhookId",
                principalTable: "CharacterWebhooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

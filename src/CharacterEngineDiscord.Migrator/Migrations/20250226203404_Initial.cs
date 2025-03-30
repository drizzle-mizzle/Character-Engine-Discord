using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedUsers",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    SpawnedCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistories", x => new { x.SpawnedCharacterId, x.CreatedAt });
                });

            migrationBuilder.CreateTable(
                name: "DiscordGuilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OwnerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    MessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MessagesSent = table.Column<long>(type: "bigint", nullable: false),
                    NoWarn = table.Column<bool>(type: "boolean", nullable: false),
                    Joined = table.Column<bool>(type: "boolean", nullable: false),
                    FirstJoinDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordGuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordUsers",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HuntedUsers",
                columns: table => new
                {
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SpawnedCharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HuntedUsers", x => new { x.DiscordUserId, x.SpawnedCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredActionType = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttemtps = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaiIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GlobalMessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CaiEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaiAuthToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaiUserId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CaiUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaiIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaiIntegrations_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscordChannels",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NoWarn = table.Column<bool>(type: "boolean", nullable: false),
                    MessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscordChannels_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildBlockedUsers",
                columns: table => new
                {
                    UserOrRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsRole = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildBlockedUsers", x => new { x.UserOrRoleId, x.DiscordGuildId });
                    table.ForeignKey(
                        name: "FK_GuildBlockedUsers_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildBotManagers",
                columns: table => new
                {
                    DiscordUserOrRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsRole = table.Column<bool>(type: "boolean", nullable: false),
                    AddedBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildBotManagers", x => new { x.DiscordUserOrRoleId, x.DiscordGuildId });
                    table.ForeignKey(
                        name: "FK_GuildBotManagers_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenRouterIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GlobalMessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenRouterModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpenRouterTemperature = table.Column<float>(type: "real", nullable: true),
                    OpenRouterTopP = table.Column<float>(type: "real", nullable: true),
                    OpenRouterTopK = table.Column<int>(type: "integer", nullable: true),
                    OpenRouterFrequencyPenalty = table.Column<float>(type: "real", nullable: true),
                    OpenRouterPresencePenalty = table.Column<float>(type: "real", nullable: true),
                    OpenRouterRepetitionPenalty = table.Column<float>(type: "real", nullable: true),
                    OpenRouterMinP = table.Column<float>(type: "real", nullable: true),
                    OpenRouterTopA = table.Column<float>(type: "real", nullable: true),
                    OpenRouterMaxTokens = table.Column<int>(type: "integer", nullable: true),
                    OpenRouterApiKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenRouterIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenRouterIntegrations_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SakuraAiIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GlobalMessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SakuraEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SakuraSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SakuraRefreshToken = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SakuraAiIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SakuraAiIntegrations_DiscordGuilds_DiscordGuildId",
                        column: x => x.DiscordGuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaiSpawnedCharacters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CallPrefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResponseDelay = table.Column<long>(type: "bigint", nullable: false),
                    FreewillFactor = table.Column<double>(type: "double precision", nullable: false),
                    FreewillContextSize = table.Column<long>(type: "bigint", nullable: false),
                    EnableSwipes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWideContext = table.Column<bool>(type: "boolean", nullable: false),
                    EnableQuotes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableStopButton = table.Column<bool>(type: "boolean", nullable: false),
                    SkipNextBotMessage = table.Column<bool>(type: "boolean", nullable: false),
                    LastCallerDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LastDiscordMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessagesSent = table.Column<long>(type: "bigint", nullable: false),
                    LastCallTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CharacterFirstMessage = table.Column<string>(type: "text", nullable: false),
                    CharacterImageLink = table.Column<string>(type: "text", nullable: true),
                    CharacterAuthor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsNfsw = table.Column<bool>(type: "boolean", nullable: false),
                    CaiTitle = table.Column<string>(type: "text", nullable: false),
                    CaiDescription = table.Column<string>(type: "text", nullable: false),
                    CaiDefinition = table.Column<string>(type: "text", nullable: true),
                    CaiImageGenEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CaiChatsCount = table.Column<int>(type: "integer", nullable: false),
                    CaiChatId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaiSpawnedCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaiSpawnedCharacters_DiscordChannels_DiscordChannelId",
                        column: x => x.DiscordChannelId,
                        principalTable: "DiscordChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenRouterSpawnedCharacters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CallPrefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResponseDelay = table.Column<long>(type: "bigint", nullable: false),
                    FreewillFactor = table.Column<double>(type: "double precision", nullable: false),
                    FreewillContextSize = table.Column<long>(type: "bigint", nullable: false),
                    EnableSwipes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWideContext = table.Column<bool>(type: "boolean", nullable: false),
                    EnableQuotes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableStopButton = table.Column<bool>(type: "boolean", nullable: false),
                    SkipNextBotMessage = table.Column<bool>(type: "boolean", nullable: false),
                    LastCallerDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LastDiscordMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessagesSent = table.Column<long>(type: "bigint", nullable: false),
                    LastCallTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CharacterFirstMessage = table.Column<string>(type: "text", nullable: false),
                    CharacterImageLink = table.Column<string>(type: "text", nullable: true),
                    CharacterAuthor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsNfsw = table.Column<bool>(type: "boolean", nullable: false),
                    CharacterStat = table.Column<string>(type: "text", nullable: false),
                    OpenRouterModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OpenRouterTemperature = table.Column<float>(type: "real", nullable: false),
                    OpenRouterTopP = table.Column<float>(type: "real", nullable: false),
                    OpenRouterTopK = table.Column<int>(type: "integer", nullable: false),
                    OpenRouterFrequencyPenalty = table.Column<float>(type: "real", nullable: false),
                    OpenRouterPresencePenalty = table.Column<float>(type: "real", nullable: false),
                    OpenRouterRepetitionPenalty = table.Column<float>(type: "real", nullable: false),
                    OpenRouterMinP = table.Column<float>(type: "real", nullable: false),
                    OpenRouterTopA = table.Column<float>(type: "real", nullable: false),
                    OpenRouterMaxTokens = table.Column<int>(type: "integer", nullable: false),
                    CharacterSourceType = table.Column<int>(type: "integer", nullable: false),
                    CardCharacterDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenRouterSpawnedCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenRouterSpawnedCharacters_DiscordChannels_DiscordChannelId",
                        column: x => x.DiscordChannelId,
                        principalTable: "DiscordChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SakuraAiSpawnedCharacters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CallPrefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessagesFormat = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResponseDelay = table.Column<long>(type: "bigint", nullable: false),
                    FreewillFactor = table.Column<double>(type: "double precision", nullable: false),
                    FreewillContextSize = table.Column<long>(type: "bigint", nullable: false),
                    EnableSwipes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWideContext = table.Column<bool>(type: "boolean", nullable: false),
                    EnableQuotes = table.Column<bool>(type: "boolean", nullable: false),
                    EnableStopButton = table.Column<bool>(type: "boolean", nullable: false),
                    SkipNextBotMessage = table.Column<bool>(type: "boolean", nullable: false),
                    LastCallerDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LastDiscordMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessagesSent = table.Column<long>(type: "bigint", nullable: false),
                    LastCallTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CharacterFirstMessage = table.Column<string>(type: "text", nullable: false),
                    CharacterImageLink = table.Column<string>(type: "text", nullable: true),
                    CharacterAuthor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsNfsw = table.Column<bool>(type: "boolean", nullable: false),
                    SakuraDescription = table.Column<string>(type: "text", nullable: false),
                    SakuraPersona = table.Column<string>(type: "text", nullable: false),
                    SakuraScenario = table.Column<string>(type: "text", nullable: false),
                    SakuraMessagesCount = table.Column<int>(type: "integer", nullable: false),
                    SakuraChatId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SakuraAiSpawnedCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SakuraAiSpawnedCharacters_DiscordChannels_DiscordChannelId",
                        column: x => x.DiscordChannelId,
                        principalTable: "DiscordChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedUsers_Id",
                table: "BlockedUsers",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaiIntegrations_DiscordGuildId",
                table: "CaiIntegrations",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CaiIntegrations_Id",
                table: "CaiIntegrations",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaiSpawnedCharacters_DiscordChannelId",
                table: "CaiSpawnedCharacters",
                column: "DiscordChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_CaiSpawnedCharacters_Id",
                table: "CaiSpawnedCharacters",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_SpawnedCharacterId",
                table: "ChatHistories",
                column: "SpawnedCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordChannels_DiscordGuildId",
                table: "DiscordChannels",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordChannels_Id",
                table: "DiscordChannels",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordGuilds_Id",
                table: "DiscordGuilds",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUsers_Id",
                table: "DiscordUsers",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildBlockedUsers_DiscordGuildId",
                table: "GuildBlockedUsers",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildBlockedUsers_UserOrRoleId_DiscordGuildId",
                table: "GuildBlockedUsers",
                columns: new[] { "UserOrRoleId", "DiscordGuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildBotManagers_DiscordGuildId",
                table: "GuildBotManagers",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildBotManagers_DiscordUserOrRoleId_DiscordGuildId",
                table: "GuildBotManagers",
                columns: new[] { "DiscordUserOrRoleId", "DiscordGuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HuntedUsers_DiscordUserId_SpawnedCharacterId",
                table: "HuntedUsers",
                columns: new[] { "DiscordUserId", "SpawnedCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenRouterIntegrations_DiscordGuildId",
                table: "OpenRouterIntegrations",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRouterIntegrations_Id",
                table: "OpenRouterIntegrations",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenRouterSpawnedCharacters_DiscordChannelId",
                table: "OpenRouterSpawnedCharacters",
                column: "DiscordChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenRouterSpawnedCharacters_Id",
                table: "OpenRouterSpawnedCharacters",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SakuraAiIntegrations_DiscordGuildId",
                table: "SakuraAiIntegrations",
                column: "DiscordGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_SakuraAiIntegrations_Id",
                table: "SakuraAiIntegrations",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SakuraAiSpawnedCharacters_DiscordChannelId",
                table: "SakuraAiSpawnedCharacters",
                column: "DiscordChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SakuraAiSpawnedCharacters_Id",
                table: "SakuraAiSpawnedCharacters",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredActions_Status_StoredActionType",
                table: "StoredActions",
                columns: new[] { "Status", "StoredActionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedUsers");

            migrationBuilder.DropTable(
                name: "CaiIntegrations");

            migrationBuilder.DropTable(
                name: "CaiSpawnedCharacters");

            migrationBuilder.DropTable(
                name: "ChatHistories");

            migrationBuilder.DropTable(
                name: "DiscordUsers");

            migrationBuilder.DropTable(
                name: "GuildBlockedUsers");

            migrationBuilder.DropTable(
                name: "GuildBotManagers");

            migrationBuilder.DropTable(
                name: "HuntedUsers");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropTable(
                name: "OpenRouterIntegrations");

            migrationBuilder.DropTable(
                name: "OpenRouterSpawnedCharacters");

            migrationBuilder.DropTable(
                name: "SakuraAiIntegrations");

            migrationBuilder.DropTable(
                name: "SakuraAiSpawnedCharacters");

            migrationBuilder.DropTable(
                name: "StoredActions");

            migrationBuilder.DropTable(
                name: "DiscordChannels");

            migrationBuilder.DropTable(
                name: "DiscordGuilds");
        }
    }
}

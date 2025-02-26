﻿// <auto-generated />
using System;
using CharacterEngineDiscord.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250226213032_OpenRouterUpdate2")]
    partial class OpenRouterUpdate2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.BlockedGuildUser", b =>
                {
                    b.Property<decimal>("UserOrRoleId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<DateTime>("BlockedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("BlockedBy")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("IsRole")
                        .HasColumnType("boolean");

                    b.HasKey("UserOrRoleId", "DiscordGuildId");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("UserOrRoleId", "DiscordGuildId")
                        .IsUnique();

                    b.ToTable("GuildBlockedUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.BlockedUser", b =>
                {
                    b.Property<decimal>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<DateTime>("BlockedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("BlockedUntil")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("BlockedUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.CharacterChatHistory", b =>
                {
                    b.Property<Guid>("SpawnedCharacterId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.HasKey("SpawnedCharacterId", "CreatedAt");

                    b.HasIndex("SpawnedCharacterId");

                    b.ToTable("ChatHistories");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordChannel", b =>
                {
                    b.Property<decimal>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("ChannelName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("MessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<bool>("NoWarn")
                        .HasColumnType("boolean");

                    b.HasKey("Id");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("DiscordChannels");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", b =>
                {
                    b.Property<decimal>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.Property<DateTime>("FirstJoinDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("GuildName")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("Joined")
                        .HasColumnType("boolean");

                    b.Property<int>("MemberCount")
                        .HasColumnType("integer");

                    b.Property<string>("MessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<long>("MessagesSent")
                        .HasColumnType("bigint");

                    b.Property<bool>("NoWarn")
                        .HasColumnType("boolean");

                    b.Property<decimal>("OwnerId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("OwnerUsername")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("DiscordGuilds");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordUser", b =>
                {
                    b.Property<decimal>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("DiscordUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.GuildBotManager", b =>
                {
                    b.Property<decimal>("DiscordUserOrRoleId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("AddedBy")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("IsRole")
                        .HasColumnType("boolean");

                    b.HasKey("DiscordUserOrRoleId", "DiscordGuildId");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("DiscordUserOrRoleId", "DiscordGuildId")
                        .IsUnique();

                    b.ToTable("GuildBotManagers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.HuntedUser", b =>
                {
                    b.Property<decimal>("DiscordUserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<Guid>("SpawnedCharacterId")
                        .HasColumnType("uuid");

                    b.HasKey("DiscordUserId", "SpawnedCharacterId");

                    b.HasIndex("DiscordUserId", "SpawnedCharacterId")
                        .IsUnique();

                    b.ToTable("HuntedUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.CaiGuildIntegration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CaiAuthToken")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CaiEmail")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CaiUserId")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("CaiUsername")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("GlobalMessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.HasKey("Id");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("CaiIntegrations");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.OpenRouterGuildIntegration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("GlobalMessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("OpenRouterApiKey")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<float?>("OpenRouterFrequencyPenalty")
                        .HasColumnType("real");

                    b.Property<int?>("OpenRouterMaxTokens")
                        .HasColumnType("integer");

                    b.Property<float?>("OpenRouterMinP")
                        .HasColumnType("real");

                    b.Property<string>("OpenRouterModel")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<float?>("OpenRouterPresencePenalty")
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterRepetitionPenalty")
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterTemperature")
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterTopA")
                        .HasColumnType("real");

                    b.Property<int?>("OpenRouterTopK")
                        .HasColumnType("integer");

                    b.Property<float?>("OpenRouterTopP")
                        .HasColumnType("real");

                    b.HasKey("Id");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("OpenRouterIntegrations");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.SakuraAiGuildIntegration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("DiscordGuildId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("GlobalMessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("SakuraEmail")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("SakuraRefreshToken")
                        .IsRequired()
                        .HasMaxLength(800)
                        .HasColumnType("character varying(800)");

                    b.Property<string>("SakuraSessionId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("DiscordGuildId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("SakuraAiIntegrations");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Metric", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("EntityId")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<int>("MetricType")
                        .HasColumnType("integer");

                    b.Property<string>("Payload")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Metrics");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.CaiSpawnedCharacter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CaiChatId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<int>("CaiChatsCount")
                        .HasColumnType("integer");

                    b.Property<string>("CaiDescription")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<bool>("CaiImageGenEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("CaiTitle")
                        .IsRequired()
                        .HasMaxLength(3000)
                        .HasColumnType("character varying(3000)");

                    b.Property<string>("CallPrefix")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterAuthor")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterFirstMessage")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("CharacterId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CharacterImageLink")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("CharacterName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<decimal>("DiscordChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("EnableQuotes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableStopButton")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableSwipes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableWideContext")
                        .HasColumnType("boolean");

                    b.Property<long>("FreewillContextSize")
                        .HasColumnType("bigint");

                    b.Property<double>("FreewillFactor")
                        .HasColumnType("double precision");

                    b.Property<bool>("IsNfsw")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("LastCallTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("LastCallerDiscordUserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("LastDiscordMessageId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("MessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<long>("MessagesSent")
                        .HasColumnType("bigint");

                    b.Property<long>("ResponseDelay")
                        .HasColumnType("bigint");

                    b.Property<bool>("SkipNextBotMessage")
                        .HasColumnType("boolean");

                    b.Property<decimal>("WebhookId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("WebhookToken")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("DiscordChannelId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("CaiSpawnedCharacters");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.OpenRouterSpawnedCharacter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AdoptedCharacterAuthorLink")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("AdoptedCharacterDefinition")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("AdoptedCharacterLink")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<int>("AdoptedCharacterSourceType")
                        .HasColumnType("integer");

                    b.Property<string>("CallPrefix")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterAuthor")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterFirstMessage")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("CharacterId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CharacterImageLink")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("CharacterName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<decimal>("DiscordChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("EnableQuotes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableStopButton")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableSwipes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableWideContext")
                        .HasColumnType("boolean");

                    b.Property<long>("FreewillContextSize")
                        .HasColumnType("bigint");

                    b.Property<double>("FreewillFactor")
                        .HasColumnType("double precision");

                    b.Property<bool>("IsNfsw")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("LastCallTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("LastCallerDiscordUserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("LastDiscordMessageId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("MessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<long>("MessagesSent")
                        .HasColumnType("bigint");

                    b.Property<float?>("OpenRouterFrequencyPenalty")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<int?>("OpenRouterMaxTokens")
                        .IsRequired()
                        .HasColumnType("integer");

                    b.Property<float?>("OpenRouterMinP")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<string>("OpenRouterModel")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<float?>("OpenRouterPresencePenalty")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterRepetitionPenalty")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterTemperature")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<float?>("OpenRouterTopA")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<int?>("OpenRouterTopK")
                        .IsRequired()
                        .HasColumnType("integer");

                    b.Property<float?>("OpenRouterTopP")
                        .IsRequired()
                        .HasColumnType("real");

                    b.Property<long>("ResponseDelay")
                        .HasColumnType("bigint");

                    b.Property<bool>("SkipNextBotMessage")
                        .HasColumnType("boolean");

                    b.Property<decimal>("WebhookId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("WebhookToken")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("DiscordChannelId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("OpenRouterSpawnedCharacters");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.SakuraAiSpawnedCharacter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CallPrefix")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterAuthor")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("CharacterFirstMessage")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("CharacterId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("CharacterImageLink")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("CharacterName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<decimal>("DiscordChannelId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<bool>("EnableQuotes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableStopButton")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableSwipes")
                        .HasColumnType("boolean");

                    b.Property<bool>("EnableWideContext")
                        .HasColumnType("boolean");

                    b.Property<long>("FreewillContextSize")
                        .HasColumnType("bigint");

                    b.Property<double>("FreewillFactor")
                        .HasColumnType("double precision");

                    b.Property<bool>("IsNfsw")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("LastCallTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("LastCallerDiscordUserId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<decimal>("LastDiscordMessageId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("MessagesFormat")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<long>("MessagesSent")
                        .HasColumnType("bigint");

                    b.Property<long>("ResponseDelay")
                        .HasColumnType("bigint");

                    b.Property<string>("SakuraChatId")
                        .HasColumnType("text");

                    b.Property<string>("SakuraDescription")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("SakuraExampleDialog")
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<int>("SakuraMessagesCount")
                        .HasColumnType("integer");

                    b.Property<string>("SakuraPersona")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<string>("SakuraScenario")
                        .IsRequired()
                        .HasMaxLength(2147483647)
                        .HasColumnType("text");

                    b.Property<bool>("SkipNextBotMessage")
                        .HasColumnType("boolean");

                    b.Property<decimal>("WebhookId")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("WebhookToken")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("DiscordChannelId");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("SakuraAiSpawnedCharacters");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.StoredAction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("Attempt")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Data")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("MaxAttemtps")
                        .HasColumnType("integer");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<int>("StoredActionType")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("Status", "StoredActionType");

                    b.ToTable("StoredActions");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.BlockedGuildUser", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordChannel", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.GuildBotManager", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.CaiGuildIntegration", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.OpenRouterGuildIntegration", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.Integrations.SakuraAiGuildIntegration", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordGuild", "DiscordGuild")
                        .WithMany()
                        .HasForeignKey("DiscordGuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordGuild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.CaiSpawnedCharacter", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordChannel", "DiscordChannel")
                        .WithMany()
                        .HasForeignKey("DiscordChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordChannel");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.OpenRouterSpawnedCharacter", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordChannel", "DiscordChannel")
                        .WithMany()
                        .HasForeignKey("DiscordChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordChannel");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters.SakuraAiSpawnedCharacter", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Domain.Models.Db.Discord.DiscordChannel", "DiscordChannel")
                        .WithMany()
                        .HasForeignKey("DiscordChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DiscordChannel");
                });
#pragma warning restore 612, 618
        }
    }
}

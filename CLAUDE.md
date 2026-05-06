# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Character Engine is a sharded Discord bot (Discord.Net 3.17, .NET 9, C#) that lets a server spawn AI-driven character webhooks backed by external chat platforms: **CharacterAI**, **SakuraAI**, **OpenRouter**, **ChubAI**. State is persisted in **PostgreSQL** via EF Core 9.

There are no tests in this repo.

## Submodules (mandatory before build)

The solution references three external clients via git submodules, and the directories under `submodules/` are **empty in fresh clones**. Build will fail until they are populated:

```bash
git submodule update --init --recursive
```

`src/CHARACTER-ENGINE-DISCORD.sln` and `CharacterEngineDiscord.Modules.csproj` reference these by relative path — do not delete or move the `submodules/` folder.

## Common commands

All commands assume working dir `src/` unless noted.

```bash
# Build & run via Docker (recommended; spins up Postgres + bot)
docker compose up --build           # from repo root; reads .env

# Local build / run
dotnet build CHARACTER-ENGINE-DISCORD.sln
dotnet run --project CharacterEngineDiscord/CharacterEngineDiscord.csproj
dotnet publish -c Release CharacterEngineDiscord/CharacterEngineDiscord.csproj -o publish

# EF Core migrations (migrations live in the Migrator project, but the
# startup project owns the connection string — both flags are required)
dotnet ef migrations add <Name> \
    --project       CharacterEngineDiscord.Migrator \
    --startup-project CharacterEngineDiscord
dotnet ef database update \
    --project       CharacterEngineDiscord.Migrator \
    --startup-project CharacterEngineDiscord
```

`Program.Main` calls `Migrator.Run(...)` on startup, so pending migrations are applied automatically on boot — manual `database update` is only needed when working offline against a local DB.

### Required runtime configuration

Two layers must be configured before the bot starts:

1. **Environment variables** (loaded from `.env` by docker-compose; copy `.env.example`):
   `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`. The connection string is built in `App/Helpers/DatabaseHelper.cs` with **`Host=db`** hard-coded — that is the docker-compose service name. To run the bot outside docker, point `db` at your Postgres host (e.g. `/etc/hosts` alias) or change `DbConnectionString` directly.
2. **`src/CharacterEngineDiscord/Settings/config.ini`** (`BotConfig` reads it at startup; copied to output via the csproj). At minimum: `BOT_TOKEN`, `ADMIN_GUILD_ID`, `ADMIN_GUILD_INVITE_LINK`, `LOGS_CHANNEL_ID`, `ERRORS_CHANNEL_ID`, `OWNER_USERS_IDS`. `BotConfig.Initialize` prefers a file starting with `env.config*` over `config*`, so you can drop in `Settings/env.config.local.ini` for a personal override without touching the tracked file.

## Architecture

### Solution layout

| Project | Role |
|---|---|
| `CharacterEngineDiscord` | Entry point (`OutputType=Exe`, `RootNamespace=CharacterEngine`). Hosts the Discord shard, DI container, handlers, repositories, services, slash command modules, helpers. |
| `CharacterEngineDiscord.Domain` | EF Core entities + `AppDbContext`. **Note**: `AppDbContext`'s namespace is `CharacterEngineDiscord.Models` (not `.Domain.Models`) — keep the existing `// ReSharper disable once CheckNamespace` annotation when editing. |
| `CharacterEngineDiscord.Migrator` | Owns the EF migrations assembly (`AppDbContext.OnConfiguring` calls `options.MigrationsAssembly("CharacterEngineDiscord.Migrator")`). `Migrator.Run` is invoked synchronously from `Program.Main`. |
| `CharacterEngineDiscord.Modules` | One module per integration (chat / search), plus character adapters and the embedded `ChubAiClient`. Depends on the three submodule clients. |
| `CharacterEngineDiscord.Shared` | Provider-agnostic DTOs and abstractions (`ICharacter`, `IIntegration`, `CommonCharacter*`). No EF / Discord deps — keep it that way. |
| `submodules/{CharacterAI,OpenRouter,SakuraAI}-Net-Client` | Upstream HTTP clients for each platform. |

### Bot lifecycle (sharded)

`Program.Main` → loads NLog config → applies migrations → caches characters/users → `CharacterEngineBot.RunAsync` constructs a `DiscordShardedClient`. **Each shard gets its own `CharacterEngineBot` and DI `ServiceProvider`** (see `_instatnces` and `CongifureShard` — both names are misspelled in source; do not silently rename, they're referenced throughout). On `ShardReady`:

- Handlers (`MessagesHandler`, `ButtonsHandler`, `ModalsHandler`, `SlashCommandsHandler`, `InteractionsHandler`, `SpecialCommandsHandler`, `BotAdminCommandsHandler`) and repositories are registered as **transients** and resolved per-event.
- Only the shard that owns `ADMIN_GUILD_ID` boots `WatchDog` and `BackgroundWorker`.
- Slash commands are registered per-guild in chunks of 5 with a 1 s/chunk throttle to avoid Discord rate limits. The admin guild gets the full set plus the explicit admin commands; other guilds initially get just `/start`, then `/disable`, then the full module set after `/start` runs.

### Provider integration model

Three concepts per platform:

1. **Module** (`Modules/Modules/...`): wraps the upstream client via `ModuleBase<TClient>`. Implements `IChatModule` (`CallCharacterAsync`) and/or `ISearchModule` (`SearchAsync`, `GetCharacterInfoAsync`). All four modules are exposed as singletons by `App/Services/IntegrationsHub.cs`; resolve them via `IntegrationsHub.GetChatModule(IntegrationType)` / `GetSearchModule(...)` rather than `new`-ing them.
2. **Adapter** (`Modules/Adapters/*CharacterAdapter.cs`): maps platform DTO ↔ shared `CommonCharacter` and `ICharacter*` interfaces.
3. **DB rows**: `*GuildIntegration` (per-guild credentials/defaults) and `*SpawnedCharacter` (a character bound to a channel + Discord webhook). Add a new provider by extending `IntegrationType` (`Shared/Enums.cs`), adding both DB types under `Domain/Models/Db/{Integrations,SpawnedCharacters}/`, registering them in `AppDbContext`, generating a migration in the Migrator project, then plugging the module into `IntegrationsHub`.

`OpenRouterModule` is special-cased: it builds chat history from the `CharacterChatHistory` table itself, so it takes a connection string + default system prompt at construction.

### Message flow

`MessagesHandler.HandleMessageAsync` dispatches every guild message:

1. `WatchDog.ValidateUser` short-circuits blocked/rate-limited users (`USER_RATE_LIMIT` interactions per 30 s; escalating blocks via `USER_FIRST_BLOCK_MINUTES` → `USER_SECOND_BLOCK_HOURS`).
2. The pre-loaded `CacheRepository.CachedCharacters` for the channel is filtered, then four call-paths run concurrently: reply-to-character, prefix match, "freewill" RNG, "hunted user" subscription.
3. Each call goes through `CallCharacterAsync` → semaphore-guarded DB reload → optional context window build (when `EnableWideContext`/`FreewillContextSize > 0` and the call is indirect) → `IntegrationsHub.GetChatModule(...).CallCharacterAsync` → reply via the cached `DiscordWebhookClient` (`CachedWebhookClientsStorage`).

Per-character settings cascade: `SpawnedCharacter.X` ?? `DiscordChannel.X` ?? `DiscordGuild.X` ?? `BotConfig.DEFAULT_X` (see `MessagesFormat` lookup in `MessagesHandler`, and the `OpenRouter*` settings in `OpenRouterModule`).

### Caching layer

`CacheRepository` is a transient that wraps three process-wide `ConcurrentDictionary` storages plus typed sub-storages (`CachedCharacerInfoStorage` [sic], `CachedWebhookClientsStorage`, `ActiveSearchQueriesStorage`). `BackgroundWorker.ClearCache` evicts entries older than 5–10 minutes. Webhook clients are cached because constructing `DiscordWebhookClient` performs a Discord round-trip; reuse them.

### Background work & stored actions

`App/Services/BackgroundWorker.cs` launches four loops on a single shard (admin shard):

| Loop | Cooldown | Purpose |
|---|---|---|
| `RunStoredActions` | 20 s | Picks up `StoredActions` rows in `Pending`, dispatches by `StoredActionType`. Currently only `SakuraAiEnsureLogin` (polls SakuraAI for email-confirmation completion). Increment `Attempt`; cancel after `MaxAttemtps`; finalizer notifies the originating channel. |
| `MetricsReport` | 1 h | Posts the `Metric` table delta to the logs channel. |
| `RevalidateBlockedUsers` | 1 min | Unblocks users whose `BlockedUntil` has elapsed. |
| `ClearCache` | 5 min | Cache eviction. |

To add a new async, retryable action: add an enum case to `StoredActionType`, write a creator helper in `StoredActionsHelper`, add a `switch` arm in both `_quickJobActionTypes` filter and the dispatch `switch` inside `RunStoredActions`, and write the give-up finalizer if the user needs to be told about timeout.

## Conventions worth preserving

- **`AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`** is set in `Program.Main` — DB code stores `DateTime.Now` (local kind) and relies on this. New entities should follow the same pattern; do not introduce `DateTime.UtcNow` without auditing all comparisons.
- Compile-time safety: every project enables `Nullable`, treats **`CS8509` (non-exhaustive switch) as error**, and silences `CS8524`. When you `switch` on an enum/`IntegrationType`, supply a default arm that throws `ArgumentOutOfRangeException` (the existing pattern in `IntegrationsHub`).
- `BotConfig` settings are split between `static readonly` (require restart) and getter-properties (`=> GetParamByName<T>(...)`) which **hot-reload** from the file on every read. Choose accordingly when adding new keys.
- Heavy paths in `MessagesHandler` use a private `SemaphoreSlim(1,1)` plus `.GetAwaiter().GetResult()` inside the lock to serialize EF calls on the per-handler `AppDbContext`. Don't switch them to `await` inside the lock without rethinking the contention model.
- Discord errors that should reach the user surface as `UserFriendlyException` (`App/Exceptions`); `MessagesHandler` and the global `UnobservedTaskException` hook silence them.
- Logging uses the `_log` instance per class; admin alerts go through `DiscordSocketClient.ReportLogAsync` / `ReportErrorAsync` extensions in `App/Helpers/Discord`.

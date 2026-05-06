# Карта архитектуры Character Engine

> Этот документ — «как код устроен, что обманчиво, что хрупко». Бизнес-логика лежит в `docs/BUSINESS_LOGIC.md`.
>
> Ссылки на код везде указаны как `путь/файл.cs:строка` или `файл.cs:метод`, чтобы можно было сразу попасть в место.

---

## 0. Карта в одном абзаце

Sharded Discord-бот на Discord.Net 3.17 + EF Core 9 + PostgreSQL. Точка входа — `CharacterEngineDiscord.csproj` (`OutputType=Exe`, `RootNamespace=CharacterEngine`). На старте читает `Settings/config.ini`, прогоняет EF-миграции через `Migrator.Run`, прогревает кэш персонажей/юзеров и поднимает `DiscordShardedClient`. На каждое событие `ShardReady` создаётся **новый `CharacterEngineBot` со своим DI-контейнером**. Slash-команды разделены на два пути: основной (через `InteractionService` + `[SlashCommand]`-классы) и explicit (через `SocketSlashCommand` событие + ручные хэндлеры для `/start`, `/disable`, admin-команд). Ответы персонажей идут не от бота, а от **кэшированных `DiscordWebhookClient`-ов** — по одному на каждого `*SpawnedCharacter`. Состояние частично в БД (`AppDbContext`), частично в process-wide статических `ConcurrentDictionary` (через классы-фасады `*Storage`). Фоновая работа (5-минутный cleanup кэша, ежечасные метрики, retry email-логина SakuraAI, разбан) — `BackgroundWorker` запускается **только на одном «admin-shard»**.

---

## 1. Bootstrap и контракт с Discord

### 1.1 Стартовая последовательность

`Program.Main` (`Program.cs:20-55`) делает следующее **синхронно и в этом порядке**:

1. Читает `Settings/NLog.config` и устанавливает его как `LogManager.Configuration` — глобальный логгер на весь процесс.
2. Логирует имя выбранного config-файла (`env.config*` приоритетнее `config*`).
3. Регистрирует три process-wide хука:
   - `AppDomain.ProcessExit` — пишет «Stopped» в лог.
   - `AppDomain.UnhandledException` — лог-only.
   - `TaskScheduler.UnobservedTaskException` — лог, кроме `UserFriendlyException`.
4. **`AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`** — критическая строка. Включает старое поведение Npgsql, при котором `DateTime` без `Kind=Utc` сохраняется как `timestamp without time zone`. Весь код использует `DateTime.Now` (локальный), и без этой строки Npgsql начнёт бросать исключения при `SaveChangesAsync`.
5. `Migrator.Run(connectionString)` — синхронно (через `.GetAwaiter().GetResult()`) применяет все pending EF-миграции. Migrations-сборка — `CharacterEngineDiscord.Migrator` (отдельный проект, см. `AppDbContext.OnConfiguring:25`).
6. `MetricsWriter.Write(MetricType.ApplicationLaunch)` — **fire-and-forget асинхронная** запись в БД. Если БД ещё не готова — упадёт молча в логи.
7. `await CharacterEngineBot.RunAsync()` — основная точка ассинхронной жизни бота (никогда не возвращается, в конце `await Task.Delay(-1)`).

### 1.2 Конфигурация Discord-клиента

`CharacterEngineBot.RunAsync` (`CharacterEngineBot.cs:157-196`) создаёт ровно один `DiscordShardedClient`:

```csharp
new DiscordSocketConfig {
    MessageCacheSize = 10,
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.GuildMessageReactions
                   | GatewayIntents.MessageContent,
    ConnectionTimeout = 30_000,
    DefaultRetryMode = RetryMode.RetryRatelimit,
    MaxWaitBetweenGuildAvailablesBeforeReady = (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
}
```

Что это значит для Discord-стороны:

| Параметр | Что ожидает Discord |
|---|---|
| `Guilds` | Базовые события `GuildAvailable`, `JoinedGuild`, `LeftGuild`. Без него бот не видит серверы. |
| `GuildMessages` | Получать `MessageReceived` для гильдий (не DM). |
| `GuildMessageReactions` | Получать `ReactionAdded` (но в коде не используется — лишний интент, можно удалить). |
| `MessageContent` | **PRIVILEGED INTENT**. Должен быть включён в Discord Developer Portal → Bot → Privileged Gateway Intents. Без него `socketMessage.Content` будет пустой и весь триггер по prefix/freewill/hunted сломается молча. |
| `MessageCacheSize=10` | Discord.Net держит в памяти последние 10 сообщений на канал — нужно для `ReferencedMessage` при reply-триггере. Если 11+ ответов на старые сообщения — `ReferencedMessage` будет null и reply-триггер не сработает. |
| `RetryRatelimit` | Discord.Net автоматически ждёт `Retry-After` и повторяет. Это снимает с кода обязанность ловить 429. |
| `MaxWaitBetweenGuildAvailablesBeforeReady=5min` | Для крупных ботов: дать всем гильдиям загрузиться, прежде чем считать shard «ready». Иначе на бота с 1000+ серверов можно получить ложный ready. |

Login: `LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN)` → `StartAsync()`. Если есть `PLAYING_STATUS` — `SetGameAsync`. Затем `Task.Delay(-1)` бесконечно, чтобы процесс не завершился.

### 1.3 Discord-разрешения (per-channel)

`ValidationsHelper.REQUIRED_PERMS` (`ValidationsHelper.cs:140-143`) — фиксированный список permission-ов, которые должны быть у бота в каждом текстовом канале, где он работает:

```
ViewChannel, SendMessages, AddReactions, EmbedLinks, AttachFiles,
ManageWebhooks, CreatePublicThreads, CreatePrivateThreads,
SendMessagesInThreads, ManageThreads, UseExternalEmojis
```

Самые важные:

- **`ManageWebhooks`** — без неё `/character spawn` упадёт на `CreateWebhookAsync`. Это сердце продукта.
- **`CreatePublicThreads`/`ManageThreads`** — нужны для авто-сплита длинных ответов в thread (`ActiveCharacterDecorator.SendMessageAsync` создаёт thread `[MESSAGE LENGTH LIMIT EXCEEDED]`).

`[ValidateChannelPermissions]` precondition атрибут проверяет это перед каждой `[SlashCommand]` (исключение — `/channel no-warn`, см. `ValidationAttributes.cs:47`). На сервере с `NoWarn=true` проверка не падает, а просто пропускается.

### 1.4 Auth-флоу с upstream-платформами

Бот **не** использует OAuth-flows для апстрим-платформ — у каждой свой костыль:

| Платформа | Как авторизоваться | Где живёт код |
|---|---|---|
| **CharacterAI** | Email → upstream шлёт magic-link → юзер копирует URL и пастит в `/integration confirm` (НЕ кликая) → `CharacterAiClient.LoginByLinkAsync` отдаёт `(Token, UserId, Username, Email)` → пишется в `CaiGuildIntegration`. | `IntegrationManagementCommands.cs:Confirm` |
| **SakuraAI** | Email → upstream шлёт magic-link → юзер кликает (важно — в инкогнито) → бот **поллит** `_client.EnsureLoginByEmailAsync` каждые 20 с, до 25 попыток. Поллинг живёт в `StoredAction`-таблице, обрабатывается `BackgroundWorker.RunStoredActions`. | `IntegrationsMaster.SendSakuraAiMailAsync` + `EnsureSakuraAiLoginAsync` |
| **OpenRouter** | Юзер сам генерит API-ключ на сайте, пастит в модалку (валидация — должен начинаться с `sk-or`). Дополнительно дефолтная модель. | `ModalsHandler.CreateOpenRouterIntegrationAsync` |
| **ChubAI** | Anonymous — публичное search-API, без логина. Используется только как «каталог». | `ChubAiClient` (in-tree, `Modules/Clients/ChubAiClient`) |

---

## 2. Listeners и фоновые задачи

### 2.1 Слушатели Discord-событий

В `CharacterEngineBot.RunAsync` (`CharacterEngineBot.cs:157-196`) на статический `DiscordShardedClient` подписываются:

- `Log` → `OnShardLog` → пишет в NLog.
- `ShardReady(SocketClient)` → создаёт `new CharacterEngineBot(socketClient)` и кладёт в статический `_instatnces[shardId]` (опечатка сохранена). Это **единственное место** где конструируется Bot.

Внутри `CongifureShard()` (опечатка сохранена, `CharacterEngineBot.cs:69-109`) на каждый shard-клиент подписываются:

| Событие Discord.Net | Кто обрабатывает | Что делает |
|---|---|---|
| `JoinedGuild` | `OnJoinedGuild` (этот же класс) | `cacheRepo.EnsureGuildCached`, регистрирует `/start`, шлёт лог в admin-канал |
| `LeftGuild` | `OnLeftGuild` | пишет `Joined=false` в `DiscordGuild`-row, лог в admin-канал |
| `ButtonExecuted` | `ButtonsHandler.HandleButton` | парсит custom_id, диспатчит в обработчик кнопки (сейчас только `SearchQuery` — пагинация результатов) |
| `InteractionExecuted` (от `InteractionService`) | `InteractionsHandler.HandleInteraction` | **постфикс-хэндлер** для всех slash-команд, обработанных через `InteractionService`. Логирует ошибки в admin-канал, отвечает юзеру embed'ом через `RespondWithErrorAsync`. |
| `MessageReceived` | `MessagesHandler.HandleMessage` | главный «мотор» — диспатч на 4 канала вызова персонажа |
| `ModalSubmitted` | `ModalsHandler.HandleModal` | парсит custom_id (`{actionTypeInt}~sep~{data}`), диспатчит на `CreateIntegration`/`OpenRouterSettings` |
| `SlashCommandExecuted` | `SlashCommandsHandler.HandleSlashCommand` | **диспетчер двух путей**: explicit команды (`/start`, `/disable`, admin-команды) идут в их хэндлеры; всё остальное — в `_interactionService.ExecuteCommandAsync` |

Дополнительно в `CharacterEngineBot.RunAsync:171-172`:
- `AppDomain.UnhandledException` → `HandleExceptionAsync` (отправляет в Discord errors-канал).
- `TaskScheduler.UnobservedTaskException` → `HandleUnobservedTaskException` (то же).

⚠️ **Эти же два хука установлены ранее в `Program.Main:34-45`**, но без Discord-отчёта, только в лог. То есть они **дважды зарегистрированы**, оба отработают. Лишний level — кандидат на удаление.

### 2.2 Только admin-shard поднимает `WatchDog` и `BackgroundWorker`

Внутри `CongifureShard` есть `Task.Run(async () => { … })` в строках 94-108. Если текущий shard содержит гильдию с `BotConfig.ADMIN_GUILD_ID`:

- `RegisterCommandsToAdminGuildAsync(adminGuild, _interactionService)` — регистрирует все [Group]-классы + `/disable` + админ-команды на admin-гильдии.
- `WatchDog.RunAsync(_serviceProvider)` — однократно загружает `BlockedUser[]` и `BlockedGuildUser[]` в process-wide static dictionaries.
- `BackgroundWorker.Run(_serviceProvider)` — стартует 4 background-loop'а (см. ниже).

После этого, **независимо от admin-shard'а**, вызывается `RegisterCommandsToAllGuildsAsync()` — для всех остальных гильдий шарда регистрирует команды чанками по 5 штук с дросселем 1 секунда между чанками (защита от глобального rate-limit на регистрацию команд).

### 2.3 BackgroundWorker — четыре loop'а

`BackgroundWorker.Run` (`BackgroundWorker.cs:26-42`) запускает четыре цикла через `RunInLoop(jobTask, duration, log)`. Каждый цикл = `Task.Run(while(true) { … })` без отмены, сваливаются исключения в admin-канал через `ReportErrorAsync`.

| Loop | Период | Что делает |
|---|---|---|
| `RunStoredActions` | 20 с | Берёт `StoredActions[Status==Pending && Type∈whitelist]`, инкрементирует `Attempt`. На превышении `MaxAttemtps` — `Canceled` + give-up-finalizer (например, шлёт юзеру «время истекло»). Сейчас единственный whitelisted тип — `SakuraAiEnsureLogin`. |
| `MetricsReport` | 1 ч | Читает `Metrics` от последнего отчёта, строит сводку (`MessagesHelper.BuildMetricsReport`), постит в `LOGS_CHANNEL_ID`. |
| `RevalidateBlockedUsers` | 1 мин | `WatchDog.UnblockUserGloballyAsync` для всех `BlockedUser.BlockedUntil <= Now`. |
| `ClearCache` | 5 мин | Эвиктит из storages по таймауту: webhook-clients (>10 мин), search-queries (>5 мин), channels/guilds/users (>5–10 мин). |

⚠️ Все loop'ы держат **единственный** `ServiceProvider` (admin-shard'а) и через него получают `AppDbContext`/`CacheRepository`. Если admin-shard упадёт и Discord-клиент перереконнектится с другим `SocketClient`'ом — `ServiceProvider` останется старый, потому что `_running=true` блокирует повторный запуск. **Это потенциальный мёртвый процесс**.

### 2.4 Пред-загрузка состояния на старте

`CharacterEngineBot.CacheUsersAndCharacters` (`CharacterEngineBot.cs:199-240`) вызывается **до** создания `DiscordShardedClient`:

- Читает все `*SpawnedCharacters` из БД (3 таблицы) + `HuntedUsers`. Через `Parallel.ForEachAsync` кладёт в `CacheRepository.CachedCharacters`.
- Читает все `DiscordUser.Id` → `CacheRepository.CacheUser`.

⚠️ Это блокирующий старт — если в БД 100k персонажей, бот зависнет до полной выгрузки. Cancellation не предусмотрен. На разогревшемся проде это критическое место для оптимизации.

---

## 3. Карта проектов и зависимости

```
            ┌──────────────────────────────────────┐
            │   CharacterEngineDiscord (Exe)       │
            │   namespace: CharacterEngine         │ ← entry point
            │   • Program / CharacterEngineBot     │
            │   • App/Handlers (5 шт)              │
            │   • App/Services (4 шт)              │
            │   • App/Repositories                 │
            │   • App/Helpers/Masters              │
            │   • Settings/{config.ini, NLog.cfg}  │
            └─────┬────────────┬──────────────┬────┘
                  │            │              │
                  ▼            ▼              ▼
       ┌──────────────┐ ┌─────────────┐ ┌──────────────┐
       │ Domain       │ │ Migrator    │ │ Modules      │
       │              │ │             │ │              │
       │ EF entities  │ │ EF migr-s   │ │ providers    │
       │ AppDbContext │ │ Migrator.   │ │ adapters     │
       │ (ns:         │ │ Run()       │ │ ChubAiClient │
       │ ...Models)   │ │             │ │ (in-tree)    │
       └──────┬───────┘ └──────┬──────┘ └──────┬───────┘
              │                │               │
              ▼                │               ▼
       ┌──────────────┐ ◄──────┘    ┌────────────────────┐
       │ Shared       │             │ submodules/        │
       │              │ ◄────── ◄── │  CharacterAI-...   │
       │ ICharacter   │             │  SakuraAI-...      │
       │ IIntegration │             │  OpenRouter-...    │
       │ CommonChar   │             └────────────────────┘
       └──────────────┘
```

Стрелки — направления `<ProjectReference>`. Особенности:

- **`Domain`** не знает ни про Discord, ни про upstream-клиенты — чистая модель + `AppDbContext`. **Но** namespace `AppDbContext` — `CharacterEngineDiscord.Models`, не `Domain.Models` (см. `// ReSharper disable once CheckNamespace` в `AppDbContext.cs:8`). Это специально, чтобы не плодить using'ов в репозиториях. Аккуратно при переименовании.
- **`Migrator`** ссылается только на `Domain` — миграции скомпилированы в отдельную сборку, чтобы EF Core команды могли работать в pure-сompile-time без приложения.
- **`Shared`** — поверхность абстракций, без EF/Discord. `IIntegration`, `ICharacter`, `IAdoptableCharacter`, `IAdoptedCharacter`, провайдер-специфичные `ISakuraCharacter/ICaiCharacter/IOpenRouterCharacter`. Реализации делятся между Domain (DB-сущности) и Modules (адаптеры).
- **`Modules`** — провайдер-специфичный код. Зависит от Domain (для `IGuildIntegration`/`ISpawnedCharacter`), Shared (для `ICharacter` и пр.) и трёх submodule-клиентов. Имеет встроенный `ChubAiClient` (не submodule, потому что Chub предоставляет публичный HTTP API без авторизации, бот его embed'ит сам).
- **`CharacterEngineDiscord` (entry)** — единственный проект, знающий и про Discord, и про БД, и про модули. Он же владеет конфигурацией.
- **submodules** — три upstream HTTP-клиента (`CharacterAi.Client`, `SakuraAi.Client`, `OpenRouter.Client`), каждый поддерживается отдельным репо того же владельца. **Если submodule репо обновится с breaking API — сборка после `git submodule update --remote` сломается без предупреждения.** Версии не пинятся (нет `submodule.<name>.update = !`).

### 3.1 Логические «модули» внутри entry-проекта

```
App/
├─ Handlers/                  ← Discord-event entry points (раздаются по событиям из CharacterEngineBot)
│  ├─ MessagesHandler         ← 4-канал диспатч сообщений → CallCharacterAsync
│  ├─ ButtonsHandler          ← only "sq" (search query) сейчас
│  ├─ ModalsHandler           ← CreateIntegration, OpenRouterSettings
│  ├─ SlashCommandsHandler    ← 2-путный диспетчер: explicit | InteractionService
│  ├─ InteractionsHandler     ← post-execute hook для InteractionService (error reporting)
│  ├─ SlashCommands/          ← [SlashCommand]-классы (5 штук + Explicit/)
│  │  ├─ CharacterCommands       /character ...
│  │  ├─ ChannelCommands         /channel ...
│  │  ├─ GuildCommands           /server ...
│  │  ├─ GuildAdminCommands      /managers
│  │  ├─ IntegrationManagementCommands  /integration ...
│  │  ├─ MiscCommands            /misc ping
│  │  └─ Explicit/
│  │     ├─ SpecialCommandsHandler   start, disable
│  │     └─ BotAdminCommandsHandler  shutdown, blockuser, unblockuser, reportmetrics
│
├─ Services/                  ← long-running / process-wide
│  ├─ IntegrationsHub         ← static singleton-ы 4-х модулей; GetChatModule/GetSearchModule
│  ├─ BackgroundWorker        ← 4 loop'а (admin-shard only)
│  ├─ WatchDog                ← rate-limit + блокировки (static state)
│  └─ MetricsWriter           ← fire-and-forget Metric inserts
│
├─ Repositories/              ← DB-фасады (transient через DI)
│  ├─ Abstractions/RepositoryBase   ← общий IDisposable wrap над AppDbContext
│  ├─ CharactersDbRepository  ← 3-кратный switch на тип спавна
│  ├─ IntegrationsDbRepository ← аналогично
│  ├─ CacheRepository         ← смешанный: и DB-вызовы, и static dict'ы
│  └─ Storages/
│     ├─ CachedCharacerInfoStorage  (опечатка сохранена)
│     ├─ CachedWebhookClientsStorage
│     └─ ActiveSearchQueriesStorage
│
├─ Helpers/                   ← stateless static
│  ├─ CommonHelper            ← TraceId, HttpClient, exception classifier
│  ├─ DatabaseHelper          ← DbConnectionString
│  ├─ IntegrationsHelper      ← icon/color/link per IntegrationType
│  ├─ ValidationsHelper       ← ValidateAccessLevelAsync, ValidateChannelPermissionsAsync, ValidateMessagesFormat
│  ├─ StoredActionsHelper     ← serialize/parse StoredAction.Data
│  ├─ Discord/                ← Discord-специфичные форматтеры (MessagesHelper и пр.)
│  ├─ Decorators/ActiveCharacterDecorator ← (spawnedChar, webhookClient) → SendMessageAsync
│  └─ Masters/
│     ├─ IntegrationsMaster   ← orchestrates SpawnCharacterAsync / SakuraAi mail flow
│     └─ InteractionsMaster   ← format/prompt каскадный preview
│
├─ Infrastructure/BotConfig   ← config.ini reader (mixed static-readonly / lazy)
├─ CustomAttributes/ValidationAttributes ← [ValidateAccessLevel], [ValidateChannelPermissions]
├─ Exceptions/UserFriendlyException
└─ CharacterEngineBot         ← shard wiring + DI + event subscriptions
```

«Master» — местное название layer'а между repositories и handlers. По смыслу это application services / orchestrators: операции, которым нужно одновременно дёрнуть несколько таблиц и/или внешних API.

---

## 4. Использование Discord.Net — паттерны и обманчивые места

### 4.1 Два пути регистрации команд

**Путь A — `InteractionService` (декларативный)**: классы наследуют `InteractionModuleBase<InteractionContext>`, методы помечены `[SlashCommand]`/`[Group]`. На старте `_interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider).Wait()` собирает их рефлексией. На каждой гильдии вызывается `_interactionService.RegisterCommandsToGuildAsync(guildId)` — это POST в Discord API «вот мой полный список команд для guild X».

**Путь B — explicit (`SlashCommandBuilder` вручную)**: `ExplicitCommandBuilders.BuildStartCommand`, `BuildDisableCommand`, `BuildAdminCommands`. Для них `guild.CreateApplicationCommandAsync(builder.Build())` создаёт **точечно одну команду**.

**Зачем два пути.** `/start` нужно зарегистрировать ещё **до** того как админ сервера согласился использовать бота — это первый touch-point. Если бы он был в `[SlashCommand]`, его регистрация шла бы вместе со всеми остальными. Текущая схема: новой гильдии регистрируется только `/start`, а после `/start` админа — все остальные через `_interactionService.RegisterCommandsToGuildAsync(guildId)`. Это управляемое раскрытие фичей. См. `EnsureCommandsRegisteredAsync` в `CharacterEngineBot.cs:287-311`.

### 4.2 Диспетчер слэш-команд (`SlashCommandsHandler`)

Главный «свитчборд» в `SlashCommandsHandler.cs:70-143`. Логика:

1. Игнорировать не-`ITextChannel`.
2. `ValidateInteraction` (rate-limit/block) — может бросить `UnauthorizedAccessException`.
3. `commandNameCamel = command.CommandName.Replace("-", "")` — удаление дефисов делает имена команд `kebab-case` парсимыми в C# enum'ы (`SpecialCommands.start`, `BotAdminCommands.shutdown` и т.д.).
4. Если `Enum.TryParse<SpecialCommands>` — `ValidateAccessLevelAsync(GuildAdmin)` + диспатч в `SpecialCommandsHandler`.
5. Иначе если `Enum.TryParse<BotAdminCommands>` — `ValidateAccessLevelAsync(BotAdmin)` + диспатч в `BotAdminCommandsHandler`.
6. Иначе — обычный `_interactionService.ExecuteCommandAsync(context, _serviceProvider)`. Запишет метрику `NewInteraction` с распарсенными опциями.

⚠️ Проблема: ошибки из путей 4-5 идут в `catch (Exception e)` верхнего `HandleSlashCommand`, где `UnauthorizedAccessException` и `UserFriendlyException` глотаются молча (`SlashCommandsHandler.cs:57`). А **ошибки из пути 6 НЕ попадают сюда** — они ловятся `InteractionService` и вызывают `InteractionExecuted` event, который обрабатывает `InteractionsHandler`. То есть error-handling двух путей **разный**.

### 4.3 Атрибуты-precondition'ы

`Discord.Net.Interactions` поддерживает `PreconditionAttribute` — асинхронная проверка перед выполнением метода. Бот использует:

- `[ValidateAccessLevel(AccessLevel.Manager)]` — на методах команд. Блокирует не-менеджеров.
- `[ValidateChannelPermissions]` — на классах команд. Блокирует, если у бота нет прав в канале.

Они расположены в `App/CustomAttributes/ValidationAttributes.cs`. Особенность: precondition бросает **обычное** исключение (`UserFriendlyException`/`UnauthorizedAccessException`) вместо возврата `PreconditionResult.FromError(...)`. Это нестандартно — Discord.Net документирует второй способ. Текущий способ работает потому, что `InteractionsHandler.HandleInteractionAsync` ловит `ExecuteResult.Exception` и шлёт его в `RespondWithErrorAsync`. Минус — не отображается в `result.IsSuccess` правильным образом, и `MetricsWriter` помечает такие команды как «err».

### 4.4 Кнопки и custom_id

Discord-кнопки идентифицируются строкой custom_id (≤100 байт). Бот:

- Формирует custom_id как `{prefix}{COMMAND_SEPARATOR}{action}`, где `COMMAND_SEPARATOR = "~sep~"` (`CommonHelper.cs:12`).
- Парсит обратно через `IndexOf("~sep~")` — не через `ParseCustomId` helper. Каждая кнопочная подсистема имеет свой формат.

Сейчас единственный prefix — `sq` (search query). Кнопки строятся в `ButtonsHelper.BuildSearchButtons`. На клик — `ButtonsHandler.HandleButton` → `GetActionType(customId)` → `UpdateSearchQueryAsync` → пересобирает embed списка результатов или вызывает `_integrationsMaster.SpawnCharacterAsync`.

⚠️ Привязка к юзеру: `if (user.Id != sq.UserId && user.Guild.OwnerId != sq.UserId) return;` (`ButtonsHandler.cs:144`). Но это **silent return** — кнопка не сообщает «не твоя сессия», просто не реагирует. Из-за `DeferAsync` в начале — Discord подсветит «Application is thinking…», и больше ничего. Это плохой UX.

### 4.5 Модалки

Модалки в Discord.Net — `Modal` объект с `CustomId` и набором `TextInput`-ов. Бот использует:

- **Создание интеграции** (Sakura/CAI/OpenRouter): `IntegrationManagementCommands.Create` строит модалку через `ModalBuilder().BuildSakuraAiAuthModal()` и т.п. (`ModalsHelper.cs`). На сабмит — `ModalsHandler.HandleModalAsync` → `CreateIntegrationAsync` → нужный приватный метод.
- **Редактирование OpenRouter настроек**: `/character openrouter-settings` или `/server openrouter-settings` шлёт модалку с **JSON в одном `TextInput`** (Paragraph). Юзер редактирует JSON руками. На сабмит — `ModalsHandler.UpdateOpenRouterSettingsAsync` парсит через `JsonConvert.DeserializeObject<OpenRouterSettings>(jsonSettings)`.

⚠️ Custom_id формат **разный** для двух типов модалок:
- `CreateIntegration`: `{actionTypeInt}~sep~{integrationTypeInt}` — обычный двух-частный формат через `ParseCustomId`.
- `OpenRouterSettings`: `{actionTypeInt}~sep~{guid}~{settingTargetInt}` — три части, второй разделитель просто `~`. Парсится в `ModalsHandler.cs:90`. **Если когда-нибудь в data попадёт `~` — упадёт.**

### 4.6 Webhook-механика

Сердце продукта — каждый персонаж отвечает не от имени бота, а через **отдельный Discord webhook**.

- **Создание**: `IntegrationsHelper.CreateDiscordWebhookAsync` (в `Helpers/Discord/InteractionsHelper.cs:38-90`). Скачивает аватар → если >10 МБ, режет MagicScaler'ом до 600px JPEG → `channel.CreateWebhookAsync(name, stream)`. Возвращает `IWebhook` (включает `Id` и `Token` — единственный момент, когда токен виден; дальше его можно достать только из БД).
- **Обёртка для отправки**: `DiscordWebhookClient(id, token)` — конструктор делает GET-запрос проверить webhook, поэтому **дорогой**. Из-за этого кэш `CachedWebhookClientsStorage`, который держит создан­ные клиенты process-wide и эвиктит после 10 минут idle.
- **Отправка**: `webhookClient.SendMessageAsync(text, threadId: ?)` — тот же клиент работает и в parent-канал, и в любой thread под ним (через `threadId` параметр).
- **Изменение**: `webhookClient.ModifyWebhookAsync(w => { w.Name = ...; w.Image = new Image(stream); })` — для `/character edit name|avatar`. Хитрость: если в кэше клиента нет (например, после рестарта), `UpdateNameAsync`/`UpdateAvatarAsync` в `CharacterCommands.cs:683-713` **создаёт совершенно новый webhook** через `InteractionsHelper.CreateDiscordWebhookAsync`. Но при этом **не обновляет** `spawnedCharacter.WebhookId/WebhookToken` в БД. **Это баг** — после такой операции в БД лежит мёртвый webhook-id, а в канале живёт новый, не привязанный ни к чему.
- **Имя со словом `discord`**: запрещено Discord-правилами. `InteractionsHelper.cs:41-46` маскирует `o`→`о` (Cyrillic) перед созданием. Грязно, но работает.
- **Удаление**: `webhookClient.DeleteWebhookAsync()` при `/character remove` или `/integration remove removeAssociatedCharacters:true`.

### 4.7 Интенты, которые НЕ используются

`GuildMessageReactions` — заявлен в интентах, но `ReactionAdded`/`ReactionRemoved` нигде не подписан. Можно убрать.

DM-каналов бот не поддерживает: `socketUserMessage.Channel is not ITextChannel` → `UserFriendlyException("Bot can operate only in text channels")`. Это значит бот — **строго guild-only**, и DM-only функции (например, безопасный ввод email через DM вместо модалки) сейчас невозможны без рефакторинга.

---

## 5. Конкурентность и состояние

### 5.1 «Шардинг» здесь — это API-разделение, не изоляция

`DiscordShardedClient` создаёт N socket-клиентов, каждый ведёт свою порцию гильдий. На каждый — отдельный `CharacterEngineBot` со своим `ServiceProvider`. **Но** все process-wide статические storages (`_blockedUsers`, `_cachedCharacters`, `_webhookClients`, кэш каналов/гильдий, и т.д.) — общие. То есть:

- **DI per shard** — да (`AppDbContext`, `Handler`-ы, репозитории).
- **State per shard** — нет (всё статика).
- **BackgroundWorker/WatchDog** — singleton, привязан к admin-shard'у через флаг `_running`.

Когда придётся горизонтально масштабироваться (>2500 гильдий → >1 шарда), это устройство будет работать по той же причине: всё в одном процессе. **Если когда-нибудь понадобится multi-process** — придётся выносить static state в Redis или подобное.

### 5.2 Шесть слоёв синхронизации

В коде 6 разных подходов к concurrency:

| Слой | Где | Что синхронизирует |
|---|---|---|
| `ConcurrentDictionary<,>` | все `*Storage`, `WatchDog._blockedUsers` | base-уровень thread-safe мап |
| `lock(_queue)` | `CachedCharacterInfo` | per-character очередь caller'ов (5 одновременно макс, потом drop) |
| `lock(user)` | `WatchDog.Validate` | rate-limit window per user |
| `SemaphoreSlim(1,1)` (per-handler) | `MessagesHandler._semaphoreSlim` | сериализация EF-вызовов на тот же `AppDbContext` (он не thread-safe) |
| `SemaphoreSlim(1,1)` (per-repo) | `CacheRepository._dbCallsSemaphore`, `CharactersDbRepository._deletionLock` | то же по другой причине |
| `Task.Run(...)` fire-and-forget | все handler'ы | оптимизация: отвечать Discord'у быстро, работать в фоне |

⚠️ **Опасный паттерн в `MessagesHandler`** (`MessagesHandler.cs:210-218, 249-258, 276-294, 357-369, 376-384`): внутри `_semaphoreSlim.WaitAsync()` — `_repo.Method().GetAwaiter().GetResult()`. Это блокирующий синхронный вызов async-метода **под асинхронной семафор-блокировкой**. Если `Method()` где-то по цепочке захочет тот же поток (через `ConfigureAwait(true)`) — это deadlock. Сейчас работает, потому что:
1. Bot не использует SynchronizationContext (нет UI/ASP.NET).
2. Discord.Net и Npgsql везде `ConfigureAwait(false)` (по умолчанию для библиотек).

**Но** это хрупкий контракт. Любая новая зависимость, которая по неосторожности use-default-contextит, его сломает.

### 5.3 Per-character очередь

Каждый `CachedCharacterInfo` (`CachedCharacerInfoStorage.cs:67-103`) держит `List<ulong> _queue` под `lock`. Логика:

- `QueueIsFullFor(userId)` — если в очереди ≥5 или этот user уже стоит → reject.
- `QueueAddCaller` → ждать, пока `QueueIsTurnOf(userId)` (head очереди или единственный).
- Wait через `Task.Delay(500)` в цикле, max 2 минуты, потом silent drop.

Это «честная» FIFO-очередь, чтобы один upstream-чат не атаковали 10 пользователей одновременно (они бы перезаписали друг другу контекст). Но реализация наивная: `List<ulong>` под lock, busy-wait через delay.

### 5.4 Race conditions в edit-флоу

При `/character edit call-prefix` обновляются параллельно:
- `spawnedCharacter.CallPrefix` (EF-сущность, потом `UpdateSpawnedCharacterAsync`)
- `cachedCharacter.CallPrefix` (in-memory `record` в `CachedCharacterInfo`)

**Без транзакции**. Если один admin делает `/character edit` параллельно с другим, последний победит в кэше, а в БД может быть совсем иначе. Сейчас это маловероятно в реальном использовании, но при unit-тестах и нагрузочных проверках всплывёт.

### 5.5 Hot path и «5 семафоров»

В `MessagesHandler.CallCharacterAsync`:
1. `_semaphoreSlim` — для DB-вызова получения интеграции.
2. `cachedCharacter.QueueAddCaller` — лок per-character.
3. `_semaphoreSlim` ещё раз — для перечитывания `spawnedCharacter` из БД (force-reload после очереди).
4. `_semaphoreSlim` ещё раз — если `messageFormat is null`, для запроса каскадного формата.
5. `_semaphoreSlim` опять — для `UpdateSpawnedCharacterAsync` после ответа.

Между ними — actual API-call в upstream (`IntegrationsHub.GetChatModule(...).CallCharacterAsync`), который **может занимать секунды**. Это не deadlock-rasiable место, но **бутылочное горло**: вся обработка сообщений в канале сериализуется через 1-1 семафор `MessagesHandler` (он transient, но т.к. handler инстанс на каждое event — создается новый, у каждого свой семафор; то есть **один семафор на инстанс handler'а**, не глобально). Это ОК.

### 5.6 Static, который притворяется инстанс-классом

`CachedWebhookClientsStorage`, `CachedCharacerInfoStorage`, `ActiveSearchQueriesStorage`:

```csharp
public sealed class CachedWebhookClientsStorage {
    private static readonly ConcurrentDictionary<ulong, CachedWebhookClient> _webhookClients = [];
    // … inst-методы оперируют статикой
}
```

Класс зарегистрирован в DI через `CacheRepository`, инстанцируется как часть transient зависимости, **но все его инстансы пишут в одну глобальную мапу**. То есть это singleton с фасадом инстанс-класса. Минусы:

- Невозможно в unit-тестах изолировать состояние без рефлексии или process-isolation.
- Запутывает: имя `CacheRepository.CachedWebhookClients` намекает на "у меня есть свой кэш", а на самом деле кэш один.

То же самое в `WatchDog` (полностью static), `MetricsWriter` (полностью static), `IntegrationsHub` (полностью static).

---

## 6. Уникальные паттерны и наследования

### 6.1 Adapter pattern (провайдер ↔ общий слой)

Базовая иерархия в `Modules/Abstractions/Base/CharacterAdapterBase.cs`:

```
CharacterAdapterBase<T> (impl ICharacterAdapter)
├─ CharacterAdapter<T>           ← обычный character (CAI)
└─ AdoptableCharacterAdapter<T>  ← может быть «усыновлён» в OpenRouter (Sakura/Chub)
```

Конкретные адаптеры:
- `CaiCharacterAdapter : CharacterAdapter<CaiCharacter>` — упаковывает CAI DTO.
- `SakuraCharacterAdapter : AdoptableCharacterAdapter<SakuraCharacter>` — может быть и обычным Sakura-чаром, и source для OpenRouter.
- `ChubCharacterAdapter : AdoptableCharacterAdapter<ChubAiCharacter>` — только source для OpenRouter (нет своего chat-API).

Адаптер выдаёт три представления одной DTO:
1. `ToCommonCharacter()` → `CommonCharacter` (для UI и поиска).
2. `GetCharacter<TResult>()` → исходную upstream DTO (для модулей).
3. `GetCharacterDescription()/GetCharacterDefinition()` → готовый текст для отображения и system-prompt (через `Modules/Helpers/Templates.cs`).

⚠️ `GetCharacter<TResult>()` использует `Convert.ChangeType(Character, typeof(TResult))!` — для DTO это no-op cast, но семантически странно. Compiler не проверит, что TResult совместим с `T`. Кандидат на замену через `(TResult)(object)Character`.

### 6.2 Modules pattern (`ModuleBase<TClient>`)

```csharp
public abstract class ModuleBase<TClient> where TClient : new() {
    protected readonly TClient _client = new();
}
```

Очень тонкий «базовый класс»: его единственная функция — заставить TClient иметь default ctor и инициализировать поле. Используется в:

- `CaiModule : ModuleBase<CharacterAiClient>` — ОК, default ctor норм.
- `SakuraAiModule : ModuleBase<SakuraAiClient>` — ОК.
- `OpenRouterModule : ModuleBase<OpenRouterClient>` — **по факту параметризован через свой ctor**, который принимает `connectionString` и `defaultSystemPrompt` и хранит их как поля модуля. `_client` (`OpenRouterClient`) не получает их вообще. То есть generic-параметр здесь декоративный.
- `ChubAiModule : ModuleBase<ChubAiClient>` — ОК.

Кандидат на упрощение: убрать `ModuleBase<TClient>` совсем, держать клиент явно в каждом модуле.

### 6.3 Master pattern (orchestrator слой)

`InteractionsMaster` и `IntegrationsMaster` — это «application services». Между repositories (тонкая прослойка над БД) и handlers/commands (тонкая прослойка над Discord). Смысл: операции, требующие нескольких таблиц + внешнего API + кэша.

Примеры:
- `IntegrationsMaster.SpawnCharacterAsync` — создаёт webhook, кэширует `DiscordWebhookClient`, создаёт `*SpawnedCharacter`-row, добавляет в `CachedCharacters`, пишет метрику. Откат при ошибке: удаляет webhook.
- `IntegrationsMaster.EnsureSakuraAiLoginAsync` — поллит upstream, обновляет/создаёт `SakuraAiGuildIntegration`, шлёт юзеру embed.
- `InteractionsMaster.BuildCharacterMessagesFormatDisplay` — каскадный поиск формата с пометкой откуда унаследовано.

⚠️ Имя «Master» нестандартное и сейчас означает что-то вроде «координатор» — лучше переименовать в `Service` или `Orchestrator` при рефакторинге.

### 6.4 Decorator pattern (`ActiveCharacterDecorator`)

`Helpers/Decorators/ActiveCharacterDecorator.cs` — обёртка над парой `(ISpawnedCharacter, DiscordWebhookClient)` с двумя методами:
- `SendGreetingAsync` — публикует first-message с placeholder-fill.
- `SendMessageAsync` — фильтрует длину (≤2000 → SendMessageAsync; >2000 → split + thread).

Это не полноценный Decorator (нет интерфейса, нет цепочки) — скорее **фасад** над «персонаж + его канал отправки». Имя сбивает с толку.

### 6.5 Repository pattern (тонкий)

`RepositoryBase` (`Repositories/Abstractions/RepositoryBase.cs`) — просто хранит `protected AppDbContext DB` и реализует `IDisposable`/`IAsyncDisposable`. Его дети:

- `CharactersDbRepository` — три параллельных запроса в три таблицы (`Sakura/Cai/OpenRouter` SpawnedCharacters), потом merge. Каждый метод — копи-паст с заменой типа. Кандидат на унификацию (например, через generic метод `GetAll<T>()` или единую таблицу).
- `IntegrationsDbRepository` — то же самое, но 3 типа интеграций.
- `CacheRepository` — особняком: расширяет RepositoryBase (получает DbContext), но при этом **держит static dictionaries** для каналов/гильдий/юзеров и **под-storages** для characters/webhooks/searches. То есть это и DB-доступ, и in-memory-кэш одновременно. Это плохо разделено — кандидат на распилку на `CacheRepository` (только кэш) и отдельный `DiscordEntitySyncService` (или подобное).

### 6.6 Extension methods как «полу-fluent» API

В `Helpers/Discord/MessagesHelper.cs` сильно много extension method'ов:
- `IDiscordClient.ReportLogAsync(...)`, `IDiscordClient.ReportErrorAsync(...)` — расширяют клиент Discord.Net прикладной семантикой («запостить в admin-канал»). Нестандартно.
- `string.ToInlineEmbed(color, ...)` — превращает строку в `Embed`.
- `Exception.ValidateUserFriendlyException()`, `Exception.ValidateWebhookException()` — классификаторы исключений (см. §7).
- `bool.ToToggler()` → `"enabled"|"disabled"`.
- `DateTime.Humanize()` → форматтер.
- `string.SplitWordsBySep(char)` — разбивает CamelCase на слова.

Это сахар, но затрудняет навигацию (extension method в C# не ищется через "find usages" типа `IDiscordClient`). Кандидат на перенос в обычные helper'ы при рефакторинге.

### 6.7 Каскадный полиморфизм через интерфейсы

Иерархия для «персонажа»:

```
ICharacter  (Shared.Abstractions.Characters)
├─ IAdoptableCharacter             ← может быть adopted в OpenRouter
│  ├─ ISakuraCharacter (Sakura)
│  └─ IChubCharacter   (Chub)
├─ ICaiCharacter                    ← обычный CAI
├─ IOpenRouterCharacter
│  └─ IAdoptedCharacter             ← реально загруженный в OpenRouter
│     • AdoptedCharacterSourceType (Sakura|Chub)
│     • AdoptedCharacterSystemPrompt?
│     • AdoptedCharacterDefinition / Description / Link / AuthorLink
└─ ISpawnedCharacter (Domain.Models.Abstractions)  ← привязка к Discord webhook
```

Доменные сущности БД реализуют пересечения: `OpenRouterSpawnedCharacter : IOpenRouterCharacter, ISpawnedCharacter`, `SakuraAiSpawnedCharacter : ISakuraCharacter, ISpawnedCharacter` и т.д. Это даёт два направления полиморфизма:

- По «what kind of webhook» (`ISpawnedCharacter` view): для логики message-flow.
- По «what kind of upstream» (`ISakuraCharacter`/`IOpenRouterCharacter` view): для модулей.

⚠️ Минус: `OpenRouterSpawnedCharacter` тащит и провайдерские поля (Temperature, TopP…), и adopted-поля (Definition, SystemPrompt), и базовые spawned-поля. Один класс, ~100 свойств. При рефакторинге — кандидат на разбивку через композицию (`OpenRouterSpawnedCharacter` владеет вложенным `OpenRouterSettings` и `AdoptedCharacterCard`).

---

## 7. Cross-cutting concerns

### 7.1 Обработка ошибок — три уровня

**Уровень 1 — process-wide hooks** (`CharacterEngineBot.cs:171-172`):
- `AppDomain.UnhandledException` → `HandleExceptionAsync` → `DiscordClient.ReportErrorAsync(... "UnhandledException")`.
- `TaskScheduler.UnobservedTaskException` → `HandleUnobservedTaskException` → то же, но игнорирует `UserFriendlyException`.

(Дублируются в `Program.Main:34-45`, см. §2.1.)

**Уровень 2 — handler-обёртки**: каждый из `MessagesHandler`, `ButtonsHandler`, `ModalsHandler`, `SlashCommandsHandler` оборачивает реальную логику в `Task.Run(async () => { try { … } catch { … } })`. Все они:
- Ловят `OutOfMemoryException` → `Environment.Exit(666)` (нестандартный код, означает «кончился heap, перезагрузи меня»).
- Ловят `UnauthorizedAccessException`/`UserFriendlyException` → silent return (`MessagesHandler`, `SlashCommandsHandler`) или sending к юзеру (`ButtonsHandler`, `ModalsHandler`).
- Прочее → `_discordClient.ReportErrorAsync(...)` + (для интерактивных) ответ юзеру через `InteractionsHelper.RespondWithErrorAsync`.

**Уровень 3 — классификатор бизнес-исключений** (`CommonHelper.ValidateUserFriendlyException`):

```csharp
e is UserFriendlyException
  or ChatModuleException
  or SakuraException
  or CharacterAiException
  or HttpRequestException
```

Эти 5 типов считаются «дружелюбными» — содержат текст для юзера. Остальное — «something went wrong» с trace-id.

`InteractionsHelper.RespondWithErrorAsync` использует этот классификатор и пытается ответить юзеру **четыре раза подряд** (RespondAsync → FollowupAsync → ModifyOriginalResponseAsync → channel.SendMessageAsync), глотая исключения каждой попытки. Это защита от того, что Discord interaction может быть в любом из 4 состояний на момент ошибки.

⚠️ `ChatModuleException` определён в `Modules/ChatModuleException.cs` — однострочный класс. По смыслу это «upstream вернул нам что-то невалидное» (см. `OpenRouterModule.cs:151`: «Failed to get response from OpenRouter»). Но он **не наследует `UserFriendlyException`** — его сообщение может содержать stacktrace или внутренние детали, и оно отправится юзеру через `RespondWithErrorAsync`. Кандидат на перепроверку при рефакторинге.

### 7.2 Логи — три направления

1. **NLog (Console + File)** — из `Settings/NLog.config`, всё уровня `Trace+`. Файл `logs/{shortdate}.log` в working dir.
2. **`LOGS_CHANNEL_ID`** — Discord-канал в admin-гильдии. Туда `ReportLogAsync` шлёт «info»-события: joined/left guild, hourly metrics, успехи operation'ов.
3. **`ERRORS_CHANNEL_ID`** — Discord-канал для ошибок. `ReportErrorAsync` создаёт **thread под error-сообщением** с заголовком `[{traceId}] {title}` и постит туда stack trace, разбитый на чанки ≤1990 символов.

`MessagesHelper.ReportErrorAsync/ReportLogAsync` (extension methods на `IDiscordClient`) — единственная точка отправки в Discord-каналы. Все catch-блоки в коде идут сюда.

⚠️ Если бот ещё не connected к Discord (например, процесс падает на старте) — `ReportErrorAsync` упадёт молча в catch, но в NLog-файле след останется.

### 7.3 Метрики

Кастомная таблица `Metric` (см. `Domain/Models/Db/Metric.cs`):

```csharp
{ Id, MetricType, EntityId?, Payload?, CreatedAt }
```

11 типов событий (`MetricType` enum), от `ApplicationLaunch` до `NewInteraction`/`CharacterCalled`/`UserBlocked`. Запись через `MetricsWriter.Write(...)` — fire-and-forget с `_ = WriteAsync(...)`. Каждый вызов **открывает свой `AppDbContext`** через `new AppDbContext(DatabaseHelper.DbConnectionString)`.

⚠️ В нагрузочной ситуации (1000 сообщений/мин) это будет бутылочное горло на connection-pool: каждое сообщение → 1+ запись в `Metric` → 1 коннект. На проде это место **точно требует батчинга** (например, в memory-buffer + flush раз в 10 сек) или хотя бы переиспользования `AppDbContext`.

Поверх Metrics строятся два отчёта:
- **Часовой авто-отчёт** — `BackgroundWorker.MetricsReport` каждый час берёт delta (с момента последнего отчёта), агрегирует и постит в logs-канал.
- **Запросный отчёт** — `BotAdminCommandsHandler.ReportMetricsAsync` (admin-команда) — c фильтром по диапазону.

Парсинг payload'а в обоих местах через `string.Split(':')` — поэтому изменение формата payload'а в любом месте (например, `MetricsWriter.Write(MetricType.NewInteraction, userId, "Button:123:456")`) ломает отчёты молча.

### 7.4 Конфигурация (`BotConfig`)

`App/Infrastructure/BotConfig.cs` — статический класс-фасад над `Settings/config.ini` (или `Settings/env.config*` если такой файл найден). Особенность — два стиля свойств:

```csharp
// «Холодные» — читаются один раз на старте, требуют рестарта
public static readonly string BOT_TOKEN = GetParamByName<string>("BOT_TOKEN").Trim();
public static readonly ulong ADMIN_GUILD_ID = GetParamByName<ulong>("ADMIN_GUILD_ID");
…

// «Горячие» — читаются ИЗ ФАЙЛА КАЖДЫЙ РАЗ, подхватывают правки на лету
public static int USER_FIRST_BLOCK_MINUTES => GetParamByName<int>("USER_FIRST_BLOCK_MINUTES");
public static string DEFAULT_AVATAR_FILE => GetParamByName<string>("DEFAULT_AVATAR_FILE");
```

`GetParamByName<T>(name)` — на каждое чтение читает **весь файл** через `File.ReadAllLines(CONFIG_PATH)`. Для горячих свойств в hot-path (например, `WatchDog.Validate` дёргает `USER_FIRST_BLOCK_MINUTES`) — это I/O на каждое сообщение. На малом масштабе ОК, на большом — **миллион Read'ов config.ini в день** — кандидат на FileWatcher + cached value.

⚠️ Конфигурация **не валидируется на старте**: если `BOT_TOKEN` пустой, бот скажет это только при `LoginAsync`. Если `OWNER_USERS_IDS` содержит мусор — упадёт на `ulong.Parse` в startup, но без human-friendly сообщения.

### 7.5 Connection string и docker-lock-in

```csharp
// App/Helpers/DatabaseHelper.cs:6-8
public static string DbConnectionString =
    $"Host=db;Port=5432;…={Environment.GetEnvironmentVariable("POSTGRES_USER")};…";
```

**`Host=db` захардкожено** — это имя сервиса в docker-compose. Бот вне docker не будет работать без `/etc/hosts`-алиаса или замены строки. Это сознательный compose-lock-in, но рассматривать как тех-долг при unit-тестах и разработке без docker.

Также `DbConnectionString` это **public static field**, не readonly, не property. Можно переопределить рантайм-тестом, но это глобальный мутируемый side-effect.

---

## 8. База данных и миграции

### 8.1 Структура

`AppDbContext` декларирует **18 DbSet'ов** (`Domain/AppDbContext.cs`):

- 3× Discord-сущности (Channel, Guild, User) — кэш-метаданные + per-channel/per-guild настройки.
- 3× GuildIntegration (Sakura, Cai, OpenRouter) — креды.
- 3× SpawnedCharacter (Sakura, Cai, OpenRouter) — почти симметричные таблицы, ~30 общих полей + платформенные.
- BlockedGuildUser, BlockedUser — модерация.
- CharacterChatHistory — только OpenRouter-история.
- GuildBotManager — список юзеров/ролей с правом писать команды.
- HuntedUser — many-to-many between SpawnedCharacter и Discord User (или другим webhook'ом).
- Metric — событийный лог.
- StoredAction — отложенные/повторяемые job'ы.

⚠️ Три симметричные таблицы `*SpawnedCharacters` и три `*Integrations` — кандидат на унификацию (single-table inheritance или дискриминатор). Сейчас каждый `Get*ByIdAsync` идёт **тремя последовательными `FindAsync`** через `??` chain (`CharactersDbRepository.cs:67-70`):

```csharp
return await DB.SakuraAiSpawnedCharacters.FindAsync(id) as ISpawnedCharacter
    ?? await DB.CaiSpawnedCharacters.FindAsync(id) as ISpawnedCharacter
    ?? await DB.OpenRouterSpawnedCharacters.FindAsync(id);
```

Это до 3-х round-trip'ов в БД ради одного персонажа. На каждое сообщение в `MessagesHandler.CallCharacterAsync` оно вызывается **дважды** (один раз для предварительной загрузки, второй — force-reload после очереди). Существенный кандидат на оптимизацию.

### 8.2 Миграции

Миграции живут в отдельном проекте `CharacterEngineDiscord.Migrator`, потому что `AppDbContext.OnConfiguring:25` явно указывает `options.MigrationsAssembly("CharacterEngineDiscord.Migrator")`. Это нужно для двух вещей:
1. Чтобы `dotnet ef` мог работать с pure-compile-time артефактом.
2. Чтобы `Migrator.Run(connectionString)` в `Program.Main` мог вызвать `db.Database.MigrateAsync()` с правильным path'ом.

**Особенность**: `CharacterEngineDiscord` (entry, OutputType=Exe) **ссылается на `Migrator`**, а не наоборот. Migrator не зависит от entry-проекта, но содержит migrations + одностраничный класс `Migrator`. На вид смешно (зачем целый проект ради 30 строк кода?), но это нужно для `dotnet ef` (он стартует через `--startup-project CharacterEngineDiscord`, а миграции читает из `--project CharacterEngineDiscord.Migrator`).

Сейчас 8 миграций (`Migrator/Migrations/2025*.cs`):
- `Initial` (Feb 2025)
- `OpenRouterUpdate`, `OpenRouterUpdate2` — добавление OpenRouter-полей
- `Add_AdoptedCharacterDescription`
- `Add_SystemPrompt`
- `ChatHistory_PK`, `ChatHistory_Identity`, `ChatHistory_Index` — итеративная подстройка `CharacterChatHistory` (PK поменян, Identity добавлен, индекс добавлен)
- `SystemPrompts`

`Migrator.Run` синхронен (`.GetAwaiter().GetResult()`). Если миграция упадёт — процесс не запустится. Это ОК.

### 8.3 Особенности EF-вызовов

- **`Update(entity)` повсеместно** в репозиториях. Это пометит ВСЕ свойства как modified, EF сгенерирует UPDATE со всеми колонками. Не оптимально, но безопасно от "stale tracker"-проблем. Для тонкой настройки можно перейти на `Attach + Entry().Property(...).IsModified = true` или просто положиться на change-tracking (если entity tracked, `SaveChangesAsync()` без явного `Update` достаточно).
- **`Include`-цепочки только в репозиториях `GetAllSpawnedCharactersInGuildAsync`** для `DiscordChannel`. В hot-path (`MessagesHandler`) — projection через `Select(new { … })`, чтобы не загружать всю entity (см. `MessagesHandler.cs:278-287`).
- **`AsNoTracking`** используется только в pre-load (`CharacterEngineBot.CacheUsersAndCharacters` — `db.HuntedUsers.AsNoTracking()...`, `db.DiscordUsers.AsNoTracking()...`). В остальных местах tracking включён, что усиливает требование «один DbContext = одно событие».

### 8.4 Кеш-БД-инвалидация

Сейчас инвалидация кеша при изменении БД делается **руками в каждой команде**:

```csharp
// /character edit call-prefix:
spawnedCharacter.CallPrefix = newValue;
cachedCharacter.CallPrefix = newValue;
await _charactersDbRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);
```

Если разработчик забудет обновить кэш — будет stale state. Кандидат на event-bus или `EF Core SaveChanges` interceptor.

---

## 9. Контракт с submodule-клиентами

Три клиента (`submodules/{CharacterAI,SakuraAI,OpenRouter}-Net-Client`) — отдельные репо одного автора. Контракт, который от них ожидается:

### 9.1 CharacterAI.Client

| Требуется | Где используется |
|---|---|
| `new CharacterAiClient()` | `ModuleBase<TClient>` default-ctor |
| `SearchAsync(query, authToken) → CaiCharacter[]` | `CaiModule.SearchAsync` |
| `GetCharacterInfoAsync(id, authToken) → CaiCharacter` | `CaiModule.GetCharacterInfoAsync` |
| `CreateNewChat(charId, userId, authToken) → string` (chatId) — **синхронный!** | `CaiModule.CallCharacterAsync` |
| `SendMessageToChat(CaiSendMessageInputData) → string` — **синхронный!** | `CaiModule.CallCharacterAsync` |
| `SendLoginEmailAsync(email)` | `IntegrationsMaster.SendCharacterAiMailAsync` |
| `LoginByLinkAsync(link) → AuthorizedUser{Token, UserId, Username, UserEmail, UserImageUrl}` | `IntegrationManagementCommands.Confirm` |
| `CharacterAiException` (тип) | classifier в `CommonHelper` |

⚠️ **Два метода синхронных** (`CreateNewChat`, `SendMessageToChat`). Это значит upstream-клиент держит `.GetAwaiter().GetResult()` или подобное внутри. В hot-path. Bottleneck для производительности и потенциальный deadlock-источник.

### 9.2 SakuraAI.Client

| Требуется | Где |
|---|---|
| `new SakuraAiClient()` | `ModuleBase` |
| `SearchAsync(query, allowNsfw) → SakuraCharacter[]` | `SakuraAiModule.SearchAsync` |
| `GetCharacterInfoAsync(id) → SakuraCharacter` | `SakuraAiModule.GetCharacterInfoAsync` |
| `CreateNewChatAsync(sessionId, refresh, char, msg) → SakuraChat{chatId, messages}` | `SakuraAiModule.CallCharacterAsync` |
| `SendMessageToChatAsync(sessionId, refresh, chatId, msg) → SakuraMessage{content}` | то же |
| `SendLoginEmailAsync(email) → SakuraSignInAttempt{Id, Email, Cookie}` | `IntegrationsMaster.SendSakuraAiMailAsync` |
| `EnsureLoginByEmailAsync(SakuraSignInAttempt) → SakuraAuthorizedUser{Username, UserImageUrl, SessionId, RefreshToken}` | `BackgroundWorker.RunStoredActions` |
| `SakuraException` (тип) | classifier |

### 9.3 OpenRouter.Client

| Требуется | Где |
|---|---|
| `new OpenRouterClient()` (default-ctor!) | `ModuleBase` |
| `CompleteAsync(apiKey, model, ChatMessage[], GenerationSettings) → CompletionsResponse` | `OpenRouterModule.CallCharacterAsync` |

`ChatMessage{Role, Content}`, `GenerationSettings{Temperature, TopP, TopK, FrequencyPenalty, PresencePenalty, RepetitionPenalty, MinP, TopA, MaxTokens}`, `CompletionsResponse{Choices[]{Message{Content}}}`. Семантика — стандартный OpenAI-compatible chat completion.

⚠️ Также упоминается **`UniversalOpenAi.Client.Models.ChatMessage`** в `OpenRouterModule.cs:14` — то есть OpenRouter.Client внутри использует ещё одну зависимость `UniversalOpenAi.Client`. При unit-тестировании потребуется мокать вглубь.

### 9.4 ChubAI client (in-tree)

`Modules/Clients/ChubAiClient/ChubAiClient.cs` (~106 строк, прямой `HttpClient` к `https://gateway.chub.ai`). Не submodule, потому что Chub имеет публичный search-API без авторизации. Это даёт **точку контроля** — можно мокать без submodule-обновления.

### 9.5 Что значит для рефакторинга

- Submodules — **public-contract зависимости**, которые ты не контролируешь монорепо-trick'ами. При апгрейде одного — может сломаться весь модуль того провайдера.
- Все 4 модуля **разделяют один интерфейс `IChatModule`/`ISearchModule`**, поэтому при unit-тестировании modules можно стабить интерфейсы, не подсаживаясь на upstream.
- Но `ICharacter`/`IIntegration` маркеры в `Shared` (см. `IIntegration.cs` — пустой!) **не имеют валидируемого контракта**. Кандидат на ужесточение.

---

## 10. Хрупкие места — короткий список «осторожно при рефакторинге»

1. **Опечатки в публичных именах** (нельзя «безопасно» переименовать без полного дифа):
   - `_instatnces` (статическая мапа в `CharacterEngineBot`)
   - `CongifureShard()` (метод там же)
   - `CachedCharacerInfoStorage` (без `t`)
   - `MaxAttemtps` (поле `StoredAction`, есть в БД)

2. **`CharacterEngineBot.DiscordClient` — `public static DiscordShardedClient`**. Используется в десятках мест (`CharacterEngineBot.DiscordClient.GetChannel`, `GetUser`, `ReportLogAsync` и т.п.). Это global state, делает unit-тесты невозможными без рефакторинга.

3. **`.GetAwaiter().GetResult()` под `SemaphoreSlim`** в `MessagesHandler` — текущий компромисс между «один DbContext = один поток» и «handler async». Хрупкий, см. §5.2.

4. **Дублированные UnhandledException-хуки** в Program и Bot (см. §2.1 и §7.1). Удаляемое.

5. **`Switch`-expression без default** в `IntegrationsDbRepository.GetGuildIntegrationAsync` — компилятор сейчас пропускает, потому что это switch-expression без `_ =>` (а значит sembrato implicit `SwitchExpressionException`). Конвенция нарушена, кандидат на исправление.

6. **`StartsWith` вместо `==`** при сравнении `WebhookId` с `stringAuthorId` в `MessagesHandler.cs:138`. Должен быть просто `==` — `StartsWith` потенциально матчит больше, чем хочется.

7. **`Update()` методы на `/character edit name|avatar` оставляют БД с мёртвым WebhookId** (см. §4.6).

8. **Custom_id парсинг** — два разных формата для модалок (см. §4.5). При появлении `~` в данных упадёт.

9. **`DbConnectionString` — public mutable static** (см. §7.5).

10. **Magic numbers в `CharactersDbRepository.CreateSpawnedCharacterAsync`** — захардкоженные дефолты `ResponseDelay=3, FreewillFactor=3, FreewillContextSize=3000`, `EnableQuotes=false, EnableStopButton=true` и т.д. Не в `BotConfig`, не в БД. При смене пользовательских ожиданий — править руками.

11. **Reflection в `/integration copy`** (`IntegrationManagementCommands.cs:120-125`): `Activator.CreateInstance + foreach (PropertyInfo)`. Копирует **все** поля, включая EF-навигационные (`DiscordGuild` reference). Может тащить tracker-state. Кандидат на явный `case` на каждый тип.

12. **`Pages` вычисляется через `while` цикл** в `SearchQuery` ctor (`ActiveSearchQueriesStorage.cs:65-70`). Должно быть `(int)Math.Ceiling(Characters.Count / 10.0)`.

13. **Static dictionaries в storage-классах** (см. §5.6). Невозможно изолировать в тестах без рефакторинга.

14. **Sharding не настоящий** (см. §5.1). Если бот пересечёт 2500 гильдий — миграция на multi-process будет крупной.

15. **`GuildAdminCommands` не наследует `[ValidateChannelPermissions]`** — на нём только проверка `AccessLevel.GuildAdmin`. То есть `/managers` можно дёрнуть в канале, где у бота нет `SendMessages`. Получится ничего, но без warning'а.

---

## 11. Карта тестируемости

Отправная точка для будущего покрытия. Группировка — «насколько просто покрыть юнит-тестами **сейчас**».

### 11.1 ✅ Easy — pure functions, чистая логика

| Класс/метод | Что тестировать |
|---|---|
| `MessagesHelper.BringMessageToFormat` | placeholder substitution, ref_message wrap/unwrap, обрезка ref до 150 символов |
| `MessagesHelper.BuildMetricsReport` | агрегация по типам метрик |
| `MessagesHelper.SplitWordsBySep`, `CapitalizeFirst`, `Humanize`, `ToToggler` | базовые форматтеры |
| `Templates.BuildCharacterDescription/Definition` | маркеры, обрезка, scenario-skip |
| `Templates.FillCharacterPlaceholders/FillUserPlaceholders` | replace |
| `ValidationsHelper.ValidateMessagesFormat` | обязательность `{{msg}}`, обёртка `{{ref_*}}` |
| `IntegrationsHelper.GetIcon/GetColor/GetServiceLink/CanNsfw` | switch-mapping |
| `CommonHelper.NewTraceId` | length, hex |
| `CommonHelper.ValidateUserFriendlyException/ValidateWebhookException` | classifier |
| `WatchDog.Validate` (приватный, можно через InternalsVisibleTo) | rate-limit threshold логика |
| `ActiveSearchQueriesStorage.SearchQuery` ctor | подсчёт страниц |
| Адаптеры (`CaiCharacterAdapter`, `SakuraCharacterAdapter`, `ChubCharacterAdapter`) | mapping upstream DTO → CommonCharacter |
| `StoredActionsHelper.CreateSakuraAiEnsureLoginData` ↔ `ExtractSakuraAiLoginData` | round-trip |

→ ~15 классов / ~50 тестов **без подсадок**. Это первый эшелон — даст безопасные «золотые правила» для последующих изменений.

### 11.2 🟡 Medium — нужен InMemoryDb или test-double

| Класс | Подсадка |
|---|---|
| `CharactersDbRepository`, `IntegrationsDbRepository` | EF Core InMemory or Testcontainers Postgres |
| `InteractionsMaster` (build*Display) | InMemoryDb с пред-заполненными `DiscordGuild`/`DiscordChannel` |
| `CacheRepository.EnsureGuildCached/EnsureChannelCached` | то же + мок `IGuild`/`ITextChannel` (Discord.Net `Mock.Of<…>`) |
| `IntegrationsMaster.SpawnCharacterAsync` | моки модулей (через `IntegrationsHub` — но он static!) + InMemoryDb + мок webhook |
| `MetricsWriter.WriteAsync` | InMemoryDb |

⚠️ Подсадка `IntegrationsHub` — главное препятствие. Его `static` синглтоны модулей надо перевести в DI (`IServiceProvider.GetRequiredService<IChatModule>(IntegrationType)`). Это 1-2 часа работы и снимает крупный пласт тестов.

### 11.3 🔴 Hard — требует декомпозиции до тестов

| Класс | Что мешает |
|---|---|
| `MessagesHandler` | `CharacterEngineBot.DiscordClient` static, fire-and-forget `Task.Run`, `.GetAwaiter().GetResult()` под `SemaphoreSlim` |
| `BackgroundWorker` | static, `Task.Run` бесконечный цикл, `_running` флаг |
| `WatchDog` | static `_blockedUsers`/`_watchedUsers`/`_blockedGuildUsers` |
| `BotConfig` | file-IO, static |
| `CharacterEngineBot` | сложная wiring-логика, DI вручную, статические `_instatnces` |
| `CharacterEngineBot.CacheUsersAndCharacters` | `Task.WaitAll` + статика |
| `*Storage` (3 шт) | static `ConcurrentDictionary` поля |

→ Это **второй эшелон**, но требует **сначала рефакторинга**. Подход: 
1. Перенести static state в инстанс-классы, регистрируемые как singleton в DI.
2. Inject Discord-клиент через интерфейс (Discord.Net предоставляет `IDiscordClient`/`BaseSocketClient`).
3. Background loops — через `IHostedService` (`Microsoft.Extensions.Hosting`), с CancellationToken.

### 11.4 ⚫ Integration only — не покрывается юнит-тестами

| Что | Почему |
|---|---|
| Реальный `LoginAsync` к Discord | требует токена + сети |
| Регистрация slash-команд | то же |
| Отправка сообщения через `DiscordWebhookClient` | webhook должен реально существовать в Discord |
| `Migrator.Run` | реальный Postgres |
| Submodule HTTP-клиенты CAI/Sakura/OpenRouter | реальные внешние API |

Подход: отдельный набор integration-тестов через Testcontainers + закрытый тестовый Discord-сервер с дев-токеном.

### 11.5 Рекомендуемая последовательность покрытия

1. **Этап A — pure helpers** (§11.1): ~50 тестов за 1-2 дня. Они станут «golden truth» для рефакторинга placeholder-логики, формата сообщений, классификации ошибок.
2. **Этап B — мини-рефакторинг IntegrationsHub** в DI: ~2-4 часа, после чего открывается тестирование модулей с моком `IChatModule`/`ISearchModule`.
3. **Этап C — repositories** через InMemoryDb: ~30-50 тестов, основа для рефакторинга 3-таблицного полиморфизма.
4. **Этап D — большой рефакторинг state**: вытащить static в singleton-сервисы. Параллельно — тесты на их новую инстанс-форму.
5. **Этап E — `MessagesHandler` end-to-end** через тестовые мок-клиенты Discord.Net (`Mock.Of<IGuildUser>()`, etc.) и заменённые модули.
6. **Этап F — integration-тесты** (Testcontainers + dev-bot).

---

## 12. Точки оптимизации (для оптимизационного этапа)

Не бизнес-логика, не архитектура — это места, где код просто медленный или «лишний». В порядке impact'а:

1. **`MetricsWriter.Write` создаёт DbContext на каждый вызов** (§7.3). Батчинг даст порядок выигрыша при нагрузке.
2. **`BotConfig.GetParamByName` читает файл при каждом обращении** к hot-properties (§7.4). FileWatcher + cached value.
3. **`CharactersDbRepository.GetSpawnedCharacterByIdAsync` делает до 3 round-trip'ов** (§8.1). Объединить через `UNION ALL` или дискриминатор.
4. **`CacheUsersAndCharacters` блокирует startup** (§2.4). Сделать lazy: грузить per-channel при первом сообщении.
5. **`MessagesHandler.CallCharacterAsync` вызывает `_charactersDbRepository.GetSpawnedCharacterByIdAsync` дважды** на каждое сообщение. Force-reload после очереди можно сделать light, через projection нужных полей.
6. **`Update(entity)` помечает все колонки** (§8.3). Точечный change-tracking сэкономит на UPDATE.
7. **WebhookClient cache eviction по 10 минут idle** (`BackgroundWorker.ClearCache`). Если активный канал — каждый раз пересоздавать webhook-client дорого. Подумать про per-character TTL.
8. **`InteractionsHelper.RespondWithErrorAsync` ретраит 4 раза разными методами**. ОК для UX, но потенциальные 4 round-trip'а в Discord на ошибку.
9. **`CharacterEngineBot.CongifureShard` делает `_interactionService.AddModulesAsync(...).Wait()`** в конструкторе. Дороже всего на старте — рефлексия по сборке. Вызывается на каждый shard-ready, то есть N раз на N шардов. Для одного шарда нет смысла, но для нескольких — кэшировать список модулей.

---

## 13. Для рефакторинг-плана — рекомендации высокого уровня

Это не «делай завтра», это «когда начнёшь — учти».

1. **Сначала покрытие, потом структурные правки.** Без §11.1-§11.3 любой рефакторинг — это лотерея.
2. **Distinguish state and behavior.** Сейчас `WatchDog`, `IntegrationsHub`, `*Storage`, `MetricsWriter`, `BackgroundWorker`, `BotConfig` — все смесь поведения и состояния. Перевод в DI-singleton с явным state-объектом сделает unit-тесты тривиальными.
3. **Одна точка истины для конфигурации.** Сейчас часть в `config.ini`, часть в env, часть hardcoded в `CharactersDbRepository.CreateSpawnedCharacterAsync`. Привести к `IOptions<BotConfig>` (стандартный .NET Hosting pattern).
4. **Отказ от per-shard DI.** Один `IServiceProvider` на процесс, регистрация шардов как `IHostedService`. Снимает странности с `_instatnces`.
5. **Migrate to `Microsoft.Extensions.Hosting.IHost`.** Это даст:
   - `IHostedService` для `BackgroundWorker` (с грациозным `CancellationToken`).
   - `IHostApplicationLifetime` для `shutdown`-команды (вместо `Environment.Exit`).
   - `ILogger<T>` вместо `LogManager.GetCurrentClassLogger()` — стандарт интегрируется с Discord-каналами через custom `ILoggerProvider`.
   - `IOptions<T>` для конфигов.
6. **Унификация трёх `*SpawnedCharacters` таблиц.** Single-table inheritance с дискриминатор-колонкой. Сэкономит десятки строк свитчей.
7. **Унификация трёх `*GuildIntegrations` таблиц.** То же.
8. **Извлечь `ICharacterRenderer`** — текущая логика «как взять `ISpawnedCharacter` и отправить сообщение через webhook» размазана между `ActiveCharacterDecorator`, `MessagesHandler`, `IntegrationsMaster`. Один сервис снимет дублирование.
9. **Custom_id — общий формат.** Введение единого парсера/билдера + протобуф/кратенький JSON под `~sep~`-схему. Снимет ловушку §4.5.
10. **Webhook recovery.** Сейчас при missed webhook — создаётся новый, БД не обновляется (§4.6). Должна быть транзакция «recover webhook → update DB → reset cache».

---

## Приложение А — диаграмма потока сообщения

```
Discord.MessageReceived
        │
        ▼
MessagesHandler.HandleMessage  ─── Task.Run(...)──┐
                                                    │
        ┌──── catch UserFriendlyException → reply ──┘
        ▼
HandleMessageAsync
  ├─ skip if not SocketUserMessage
  ├─ skip if author == bot
  ├─ skip if content starts with "~ignore"
  ├─ throw if not ITextChannel
  ├─ WatchDog.ValidateUser (rate-limit / block)
  ├─ load CachedCharacters[channel] (filter out self-author)
  │
  ├─ Reply-trigger: FindCharacterByReplyAsync ──┐
  ├─ Prefix-trigger: FindCharacterByPrefixAsync ─┼─ CallCharacterAsync (direct)
  ├─ Freewill-trigger: FindRandomCharacterAsync ─┴─ CallCharacterAsync (indirect)
  ├─ Hunter-trigger: FindHunterCharactersAsync ──── CallCharacterAsync (direct, x N)
  │
  └─ Task.WaitAll(callTasks)

CallCharacterAsync(spawnedCharacter, msg, isIndirect):
  ├─ NSFW check (channel.IsNsfw vs character.IsNfsw)
  ├─ semaphore: load IGuildIntegration
  ├─ cachedCharacter.QueueAddCaller (FIFO ≤ 5)
  ├─ wait turn (max 2 min)
  ├─ semaphore: force-reload spawnedCharacter
  ├─ delay: max(5s, ResponseDelay) if author is bot/webhook else ResponseDelay
  ├─ semaphore: lookup messageFormat (cascade: char→channel→guild→default)
  ├─ build context window (if isIndirect && FreewillContextSize > 0)
  ├─ IntegrationsHub.GetChatModule(type).CallCharacterAsync(...)  ← upstream API
  ├─ ActiveCharacterDecorator.SendMessageAsync via cached webhook
  │     ├─ ≤ 2000 chars → SendMessageAsync
  │     └─ > 2000 chars → split + create thread "[MESSAGE LENGTH LIMIT EXCEEDED]"
  ├─ semaphore: update spawnedCharacter (LastCallTime, MessagesSent++, ...)
  └─ MetricsWriter.Write(CharacterCalled)
```

## Приложение Б — топ файлов по уровню тех-долга

| Файл | Строк | Долг |
|---|---|---|
| `App/Handlers/MessagesHandler.cs` | 519 | Hot-path, semaphore + GetAwaiter, static client refs, big switch |
| `App/CharacterEngineBot.cs` | 360 | Statefull static, опечатки, сложная wiring |
| `App/Handlers/SlashCommands/CharacterCommands.cs` | 781 | Огромный класс, 12 команд в одном файле, дублирование валидации |
| `App/Repositories/CharactersDbRepository.cs` | 268 | Тройной switch, hardcoded дефолты в Create |
| `App/Services/BackgroundWorker.cs` | 311 | static, fire-and-forget, _running флаг блокирует recovery |
| `App/Services/WatchDog.cs` | 241 | Полностью static API |
| `App/Repositories/Storages/CachedCharacerInfoStorage.cs` | 202 | Опечатка в имени, 5 record-классов в одном файле |
| `App/Helpers/Discord/MessagesHelper.cs` | 479 | Смешано: report-utility + format-utility + extension methods |

---

*Документ написан на основе аудита кода в коммите рабочей ветки `claude/analyze-repo-setup-5iQNw` (May 2026). Все ссылки на строки актуальны на момент написания; при существенных изменениях файлов их нужно обновить.*

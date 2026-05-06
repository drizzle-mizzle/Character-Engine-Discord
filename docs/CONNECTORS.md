# Карта коннекторного слоя Character Engine

> Этот документ — **«как основное приложение разговаривает с upstream-клиентами»**. Сами клиенты (`CharacterAi.Client`, `SakuraAi.Client`, `OpenRouter.Client`, in-tree `ChubAiClient`) рассматриваются как чёрные ящики; коннекторами называется код, который их инстанцирует, кастует, кормит и парсит результат.
>
> Связанные документы:
> - `docs/BUSINESS_LOGIC.md` — что бот делает (4 канала вызова персонажа, каскад, lifecycle).
> - `docs/ARCHITECTURE.md` — как код устроен в целом (bootstrap, sharding, DI, паттерны).
>
> Здесь — узкая поверхность: всё что лежит в `CharacterEngineDiscord.Modules` + связки в entry-проекте (`IntegrationsHub`, `IntegrationsMaster`, `CharactersDbRepository.CreateSpawnedCharacterAsync` в части коннекторов).

---

## 0. Карта в одном абзаце

Между Discord-обработчиками (`MessagesHandler`, slash-команды) и upstream-клиентами лежит трёхуровневый слой: **(1) интерфейсы** в `Shared` (`IChatModule`, `ISearchModule`, `ICharacter`, `IIntegration`, провайдер-специфичные `ISakuraCharacter`/`ICaiCharacter`/`IOpenRouterCharacter`), **(2) модули** в `Modules/Modules/{Chat,Search,Universal}/` (4 штуки — по одному на провайдер), которые наследуют `ModuleBase<TClient>` и реализуют один или оба интерфейса, **(3) адаптеры** в `Modules/Adapters/` (3 штуки), мапящие upstream-DTO в `CommonCharacter` и обратно. Все четыре модуля выставлены через **`IntegrationsHub`** — статический диспетчер с четырьмя singleton-полями. Контракт между уровнями **слабо типизирован**: `CallCharacterAsync(ICharacter, IIntegration, string)` принимает супертипы и тут же кастит к провайдер-специфичным интерфейсам внутри (runtime cast). Это сознательный компромисс — мульти-провайдерная диспетчеризация без generics, но цена — `InvalidCastException`'ы при неправильной паре `(character, integration)`.

---

## 1. Архитектурная схема коннекторов

```
                  Discord-команды и события
                  ┌────────────────────────────────────┐
                  │  MessagesHandler                   │
                  │  CharacterCommands.SpawnCharacter  │
                  │  ButtonsHandler (sq~select)        │
                  │  IntegrationManagementCommands     │
                  └─────────────────┬──────────────────┘
                                    │
                                    ▼
                ┌──────────────────────────────────────┐
                │ App/Services/IntegrationsHub         │  ← статический диспетчер
                │  • SakuraAiModule                    │     4 синглтон-поля
                │  • CharacterAiModule                 │     GetChatModule(IntegrationType)
                │  • OpenRouterModule                  │     GetSearchModule(IntegrationType|
                │  • ChubAiModule                      │                     CharacterSourceType)
                └────┬───────────┬──────────┬─────────┘
                     │           │          │
        ┌────────────┘           │          └──────────────┐
        ▼                        ▼                          ▼
┌──────────────────┐   ┌────────────────────┐   ┌────────────────────┐
│ IChatModule      │   │ ISearchModule      │   │ (provider-spec.    │
│ CallCharacter    │   │ SearchAsync        │   │  методы:           │
│ (ICharacter,     │   │ GetCharacterInfo   │   │   SendLoginEmail   │
│  IIntegration,   │   │ (id, integration)  │   │   LoginByLink      │
│  string msg)     │   │  → ICharacterAd-   │   │   EnsureLogin )    │
│  → CommonChar-   │   │    apter           │   │                    │
│    Message       │   │                    │   │                    │
└──────────────────┘   └────────────────────┘   └────────────────────┘
        │                        │                          │
        ▼                        ▼                          ▼
┌────────────────────────────────────────────────────────────────────┐
│  abstract ModuleBase<TClient>  : protected readonly TClient _client│
│       │                                                            │
│       ├─ SakuraAiModule  : ModuleBase<SakuraAiClient>,             │
│       │                    IChatModule, ISearchModule              │
│       ├─ CaiModule       : ModuleBase<CharacterAiClient>,          │
│       │                    IChatModule, ISearchModule              │
│       ├─ OpenRouterModule: ModuleBase<OpenRouterClient>,           │
│       │                    IChatModule  (NO ISearch)               │
│       │                    + connectionString, defaultSystemPrompt │
│       └─ ChubAiModule    : ModuleBase<ChubAiClient>,               │
│                            ISearchModule  (NO IChat)               │
└────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌────────────────────────────────────────────────────────────────────┐
│  abstract CharacterAdapterBase<T>                                  │
│       │                                                            │
│       ├─ CharacterAdapter<T>           ← обычный chat-character    │
│       │   └─ CaiCharacterAdapter <CaiCharacter>                    │
│       │                                                            │
│       └─ AdoptableCharacterAdapter<T>  ← может быть adopted        │
│           ├─ SakuraCharacterAdapter <SakuraCharacter>              │
│           └─ ChubCharacterAdapter   <ChubAiCharacter>              │
└────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌────────────────────────────────────────────────────────────────────┐
│  upstream-clients                                                  │
│  • SakuraAi.Client.SakuraAiClient            (submodule)           │
│  • CharacterAi.Client.CharacterAiClient      (submodule)           │
│  • OpenRouter.Client.OpenRouterClient        (submodule)           │
│  • ChubAiClient                              (in-tree, ~106 LOC)   │
└────────────────────────────────────────────────────────────────────┘
```

---

## 2. Контракты — что обязаны интерфейсы, а что свободно

### 2.1 Интерфейсы коннекторного слоя (в `Shared/Modules`)

```csharp
// Modules/Abstractions/IModule.cs
public interface IModule { }   // marker, без членов

// Modules/Abstractions/IChatModule.cs
public interface IChatModule : IModule {
    Task<CommonCharacterMessage> CallCharacterAsync(
        ICharacter character,
        IIntegration integration,
        string message);
}

// Modules/Abstractions/ISearchModule.cs
public interface ISearchModule : IModule {
    Task<List<CommonCharacter>> SearchAsync(
        string query, bool allowNsfw, IIntegration integration);

    Task<ICharacterAdapter> GetCharacterInfoAsync(
        string characterId, IIntegration integration);
}
```

Контракт **очень узкий**: всего 3 метода на 2 интерфейса + пустой маркер. Все провайдер-специфичные методы (`SendLoginEmailAsync`, `LoginByLinkAsync`, `EnsureLoginByEmailAsync`) **в интерфейсы не входят** — они объявлены прямо в классе модуля и доступны через типизированную ссылку: `IntegrationsHub.SakuraAiModule.SendLoginEmailAsync(...)`.

⚠️ Это значит: **полиморфно через `IntegrationsHub.GetChatModule(type)` доступны только `CallCharacterAsync`, `SearchAsync`, `GetCharacterInfoAsync`**. Всё остальное (login flow, OpenRouter-settings) — статически типизированные вызовы конкретного модуля. При рефакторинге это придётся либо унифицировать, либо признать как два контракта (общий + провайдер-специфичный).

### 2.2 Маркеры супертипов (в `Shared/Abstractions`)

```csharp
public interface IIntegration { }                  // pure marker
public interface IChatOnlyIntegration : IIntegration {
    string? SystemPrompt { get; }
}
public interface ICharacter {
    string  CharacterId { get; set; }
    string  CharacterName { get; set; }
    string  CharacterFirstMessage { get; set; }
    string? CharacterImageLink { get; set; }
    string  CharacterAuthor { get; set; }
    bool    IsNfsw { get; set; }
}
```

`IIntegration` — **пустой маркер** без членов. То есть на уровне сигнатур `CallCharacterAsync(ICharacter, IIntegration, string)` нет НИЧЕГО, что компилятор мог бы проверить. Тип `IIntegration` существует исключительно для документирующей роли «это интеграция».

Это сознательный приём, чтобы можно было передавать в один метод как `SakuraAiGuildIntegration`, так и `OpenRouterGuildIntegration`, и т.д., и каст'ить внутри. Цена — потеря compile-time safety.

### 2.3 Иерархия персонажа

```
ICharacter
├─ IAdoptableCharacter                 ← можно «усыновить» в OpenRouter
│   • CharacterSourceType GetCharacterSourceType()
│   ├─ ISakuraCharacter
│   │   • string  SakuraDescription { get; set; }
│   │   • string  SakuraScenario { get; set; }
│   │   • int     SakuraMessagesCount { get; set; }
│   │   • string? SakuraChatId { get; set; }   ← lazy: пишется при первом сообщении
│   └─ IChubCharacter   (без своих полей; маркер)
│
├─ ICaiCharacter                       ← обычный CAI, не усыновляется
│   • string  CaiTitle { get; set; }
│   • string  CaiDescription { get; set; }
│   • bool    CaiImageGenEnabled { get; set; }
│   • int     CaiChatsCount { get; set; }
│   • string? CaiChatId { get; set; }   ← lazy: пишется при первом сообщении
│
└─ IOpenRouterCharacter
    extends IOpenRouterConfigurable {
        Model?, Temperature?, TopP?, TopK?, FrequencyPenalty?, PresencePenalty?,
        RepetitionPenalty?, MinP?, TopA?, MaxTokens?
    }
    extends IAdoptedCharacter {
        CharacterSourceType AdoptedCharacterSourceType { get; }
        string?             AdoptedCharacterSystemPrompt { get; set; }
        string              AdoptedCharacterDefinition { get; }
        string              AdoptedCharacterDescription { get; }
        string              AdoptedCharacterLink { get; }
        string              AdoptedCharacterAuthorLink { get; }
    }
```

Ключевые наблюдения:

1. **`SakuraChatId`/`CaiChatId` — set-able через интерфейс**. Это значит, что модуль (см. `SakuraAiModule.CallCharacterAsync`, `CaiModule.CallCharacterAsync`) **мутирует переданный character**, записывая туда chatId после первого сообщения. Это побочный эффект, не отражённый в сигнатуре `CallCharacterAsync`. Отдельная модель: `OpenRouter` ничего не мутирует — у него история живёт в БД.

2. **`IAdoptable` vs `IAdopted`** — две стороны одной идеи. `IAdoptable` — character, **готовый** быть усыновлённым (Sakura/Chub каталог). `IAdopted` — character, **уже** усыновлённый (OpenRouter, держит копию данных). Это две разные роли в коннекторе.

3. **`IOpenRouterConfigurable`** — выделен отдельно, потому что его реализуют и `IOpenRouterCharacter` (per-character override), и `IOpenRouterIntegration` (per-guild defaults). Это позволяет каскад `Spawn ?? Integration` без дублирования.

### 2.4 Pair-контракт (которого нет в сигнатурах)

Все `CallCharacterAsync(ICharacter, IIntegration, string)` ожидают **строго определённую пару**:

| Модуль | Ожидаемый тип character | Ожидаемый тип integration |
|---|---|---|
| `SakuraAiModule` | `ISakuraCharacter` | `ISakuraIntegration` |
| `CaiModule` | `ICaiCharacter` | `ICaiIntegration` |
| `OpenRouterModule` | `OpenRouterSpawnedCharacter` (concrete!) | `IOpenRouterIntegration` |

Если кто-то перепутает (например, `CaiSpawnedCharacter` + `SakuraAiIntegration`) — `InvalidCastException` на первой строке `CallCharacterAsync`. **Compile-time это не ловится**.

⚠️ В коде эта инвариант защищается **`MessagesHandler`-ом**: `IntegrationsDbRepository.GetGuildIntegrationAsync(spawnedCharacter)` сам определяет тип через `spawnedCharacter.GetIntegrationType()` и достаёт правильную интеграцию. Но защита на уровне callsite'а, не на уровне типа.

⚠️ **`OpenRouterModule` уникально кастит к concrete-классу**: `(OpenRouterSpawnedCharacter)character`, не `IOpenRouterCharacter`. Это значит, что `OpenRouterModule` нельзя вызывать с любым `IOpenRouterCharacter`, отличным от EF-сущности. Минорный анти-паттерн (нарушение DIP), но это специально — модулю нужен `Id` для `db.ChatHistories.Where(... == id)`.

---

## 3. IntegrationsHub — диспетчер модулей

`App/Services/IntegrationsHub.cs` — **статический класс** с четырьмя singleton-полями (свойства-инициализаторы):

```csharp
public static SakuraAiModule SakuraAiModule { get; } = new();
public static CaiModule      CharacterAiModule { get; } = new();
public static OpenRouterModule OpenRouterModule { get; }
    = new(DatabaseHelper.DbConnectionString, BotConfig.DEFAULT_SYSTEM_PROMPT);
public static ChubAiModule   ChubAiModule { get; } = new();
```

Что важно:

1. **Все 4 — синглтоны на процесс**. Создаются при первом обращении к статике (CLR-гарантия thread-safe одноразовой инициализации static полей).
2. **Три из четырёх — default-ctor**. Только `OpenRouterModule` параметризован, потому что ему нужен connection string (он лезет в свою БД-таблицу `CharacterChatHistory`) и default prompt.
3. **Конструкторы не вызывают сетевых операций** — клиенты `new()`-ятся, но HTTP-запросы делаются только при первом `SearchAsync`/`CallCharacterAsync`. Если, например, OpenRouter-сервер недоступен, бот всё равно стартует.

API диспетчера — три метода:

```csharp
IChatModule  GetChatModule(IntegrationType integrationType);
ISearchModule GetSearchModule(IntegrationType integrationType);
ISearchModule GetSearchModule(CharacterSourceType characterSourceType);
```

Логика — простой `switch`-expression:

| Аргумент | `GetChatModule` | `GetSearchModule` |
|---|---|---|
| `SakuraAI` | SakuraAiModule | SakuraAiModule |
| `CharacterAI` | CharacterAiModule | CharacterAiModule |
| `OpenRouter` | OpenRouterModule | ⚠ throws `ArgumentOutOfRangeException` |
| `SakuraAI` (как `CharacterSourceType`) | — | SakuraAiModule |
| `ChubAI` (как `CharacterSourceType`) | — | ChubAiModule |

⚠️ **`GetSearchModule(IntegrationType.OpenRouter)` упадёт** — это сознательно: у OpenRouter нет своего search-API. Поэтому при `/character spawn type:OpenRouter` обязателен параметр `useCatalog:Sakura|ChubAI`, и в `CharacterCommands.SpawnCharacter:115-127` явно вилка:

```csharp
if (!guildIntegration.IsChatOnly) {
    searchModule = IntegrationsHub.GetSearchModule(integrationType);   // Sakura, CAI
} else if (useCatalog is CharacterSourceType sourceType) {
    searchModule = IntegrationsHub.GetSearchModule(sourceType);        // OpenRouter case
} else {
    throw new UserFriendlyException("specify the use-catalog parameter");
}
```

`IsChatOnly` — это поле `IGuildIntegration` (`true` только у `OpenRouterGuildIntegration`). Так бот знает, что для интеграции нет собственного каталога.

### 3.1 Что это значит для тестируемости

Поскольку всё статика, **в unit-тестах нельзя подменить модуль** на mock. Чтобы тестировать, скажем, `MessagesHandler.CallCharacterAsync`, придётся либо:
1. Перевести `IntegrationsHub` в DI-singleton (рекомендуемый ход).
2. Использовать `InternalsVisibleTo` + рефлексию для подмены полей (хрупко).
3. Ограничиться integration-тестами (медленно).

**Самый дешёвый шаг рефакторинга** — превратить `IntegrationsHub` в `IIntegrationsHub` интерфейс + регистрация в DI. Это снимает блокер на тесты §11.2 архитектурной карты.

---

## 4. Модули — поведение каждого

### 4.1 `SakuraAiModule` (`Modules/Modules/Universal/SakuraAiModule.cs`)

**Реализует**: `IChatModule`, `ISearchModule` + специфичные `SendLoginEmailAsync`, `EnsureLoginByEmailAsync`.

**Использует из upstream-клиента**:
- `_client.SearchAsync(query, allowNsfw) → SakuraCharacter[]`
- `_client.GetCharacterInfoAsync(id) → SakuraCharacter`
- `_client.CreateNewChatAsync(sessionId, refresh, SakuraCharacter, msg) → SakuraChat{chatId, messages[]}`
- `_client.SendMessageToChatAsync(sessionId, refresh, chatId, msg) → SakuraMessage{content}`
- `_client.SendLoginEmailAsync(email) → SakuraSignInAttempt{Id, Email, Cookie}`
- `_client.EnsureLoginByEmailAsync(SakuraSignInAttempt) → SakuraAuthorizedUser{Username, UserImageUrl, SessionId, RefreshToken}`

**Cast'ы внутри `CallCharacterAsync`** (`SakuraAiModule.cs:31-60`):
```csharp
var sakuraCharacter   = (ISakuraCharacter)character;       // мог упасть InvalidCastException
var sakuraIntegration = (ISakuraIntegration)integration;
```

**Lazy-create chat**:
```csharp
if (sakuraCharacter.SakuraChatId is null) {
    // Конструируем upstream DTO с минимумом данных
    var sakuraClientCharacter = new SakuraCharacter {
        id = character.CharacterId,
        firstMessage = character.CharacterFirstMessage,
    };
    var sakuraChat = await _client.CreateNewChatAsync(...);
    response = sakuraChat.messages.Last().content;
    sakuraCharacter.SakuraChatId = sakuraChat.chatId;   // ⚠ мутация переданного character
} else {
    var sakuraMessage = await _client.SendMessageToChatAsync(..., sakuraCharacter.SakuraChatId, message);
    response = sakuraMessage.content;
}
```

⚠️ **Мутация переданного character** — `sakuraCharacter.SakuraChatId = ...`. Это побочный эффект, не отражённый в сигнатуре `CallCharacterAsync`. Caller должен **знать**, что после успешного вызова EF-сущность нужно сохранить. Сейчас это делает `MessagesHandler.CallCharacterAsync:379` через `_charactersDbRepository.UpdateSpawnedCharacterAsync(spawnedCharacter)`. Но если в будущем кто-то вызовет модуль не из `MessagesHandler` — забудет сохранить, и chatId восстановится только после рестарта (если кэш загрузит EF-сущность с тем же значением).

⚠️ **Минимум полей при `CreateNewChatAsync`**: передаётся только `id` и `firstMessage`, остальное Sakura-сторона подгрузит сама. Если upstream API изменит требование — потребуется обновить.

**Контракт исключений**: модуль не ловит ничего. `SakuraException` (из submodule) пробрасывается наверх, классифицируется в `CommonHelper.ValidateUserFriendlyException` как «дружелюбное» и идёт юзеру embed'ом.

**`IsChatOnly`**: `false` (есть search-API).

---

### 4.2 `CaiModule` (`Modules/Modules/Universal/CaiModule.cs`)

**Реализует**: `IChatModule`, `ISearchModule` + специфичные `SendLoginEmailAsync`, `LoginByLinkAsync`.

**Использует из upstream-клиента**:
- `_client.SearchAsync(query, authToken) → CaiCharacter[]`
- `_client.GetCharacterInfoAsync(id, authToken) → CaiCharacter`
- `_client.CreateNewChat(charId, userId, authToken) → string` (chatId) — **синхронный!**
- `_client.SendMessageToChat(CaiSendMessageInputData) → string` — **синхронный!**
- `_client.SendLoginEmailAsync(email)` (async)
- `_client.LoginByLinkAsync(link) → AuthorizedUser{Token, UserId, Username, UserEmail, UserImageUrl}` (async)

**Cast'ы**: `(ICaiCharacter)character`, `(ICaiIntegration)integration`.

**Lazy-create chat**:
```csharp
if (caiCharacter.CaiChatId is null) {
    caiCharacter.CaiChatId = _client.CreateNewChat(...);   // sync!
}
var data = new CaiSendMessageInputData {
    CharacterId   = caiCharacter.CharacterId,
    ChatId        = caiCharacter.CaiChatId,
    Message       = message,
    UserId        = caiIntegration.CaiUserId,
    Username      = caiIntegration.CaiUsername,
    UserAuthToken = caiIntegration.CaiAuthToken
};
var response = _client.SendMessageToChat(data);   // sync!
return Task.FromResult(new CommonCharacterMessage { Content = response });
```

⚠️ **Два метода upstream-клиента — синхронные**. `CallCharacterAsync` декларирован как `Task<...>`, но внутри — `Task.FromResult(...)`. Реальная отправка блокирует поток, на котором живёт handler. В hot-path это **bottleneck**: пока CAI отвечает (5-15 секунд), thread занят. Рефакторинг submodule-клиента к async-API — обязательный пункт оптимизации.

**Мутация character**: то же что у Sakura — `caiCharacter.CaiChatId = ...`. Тот же контракт «caller обязан сохранить».

**Контракт исключений**: `CharacterAiException` (submodule) → классифицируется как UserFriendly. `MessagesHandler.HandleMessageAsync:174` дополнительно его перехватывает и репортит в админ-канал с эмодзи (особое отношение к CAI-ошибкам).

**`IsChatOnly`**: `false`.

---

### 4.3 `OpenRouterModule` (`Modules/Modules/Chat/OpenRouterModule.cs`)

**Реализует**: только `IChatModule`. **Не имеет search**.

**Особенный конструктор**:
```csharp
public OpenRouterModule(string connectionString, string defaultSystemPrompt) {
    _connectionString    = connectionString;
    _defaultSystemPrompt = defaultSystemPrompt;
}
```

`_client` (`OpenRouterClient`) создаётся через base-class `new TClient()` — без параметров. Параметры модуля (`connectionString`, `defaultSystemPrompt`) **не передаются клиенту**, они нужны только модулю самому.

**Использует из upstream-клиента**: единственный метод
- `_client.CompleteAsync(apiKey, model, ChatMessage[], GenerationSettings) → CompletionsResponse`

**Использует из БД**:
- Открывает свой `AppDbContext` через `new AppDbContext(_connectionString)` на каждый вызов.
- Читает `db.ChatHistories.Where(ch => ch.SpawnedCharacterId == orSpawnedCharacter.Id)`.
- Дописывает новые `CharacterChatHistory` row'ы (system / user / assistant).

**Cast'ы** (`OpenRouterModule.cs:35-37`):
```csharp
var orIntegration       = (IOpenRouterIntegration)integration;
var orSpawnedCharacter  = (OpenRouterSpawnedCharacter)character;   // ⚠ concrete class!
```

⚠️ **Кастит к concrete EF-сущности**, не к `IOpenRouterCharacter`. Причина — нужен `.Id` (Guid) для запроса в `ChatHistories`. Это нарушает DIP, и формально означает: `OpenRouterModule` нельзя вызвать с in-memory DTO, только с EF-row.

**Системный prompt — каскад**:
```csharp
var prompt = (orSpawnedCharacter.AdoptedCharacterSystemPrompt
              ?? orIntegration.SystemPrompt
              ?? _defaultSystemPrompt)
             .FillCharacterPlaceholders(orSpawnedCharacter.CharacterName);

var systemPrompt = $"{prompt}\n{orSpawnedCharacter.AdoptedCharacterDefinition}";
```

То есть system-prompt = (jailbreak/instructions) + `\n` + (character-card definition). Первая часть может быть переопределена per-character, per-integration, или дефолт. Вторая часть всегда из adopted character.

**Инициализация истории**:
```csharp
if (history.Count == 0) {
    // Первое сообщение — добавляем system + assistant (greeting) в БД
    var systemMessage   = new CharacterChatHistory { Name = "system",    Message = systemPrompt };
    var firstMessage    = new CharacterChatHistory { Name = "assistant", Message = orSpawnedCharacter.CharacterFirstMessage };
    db.ChatHistories.AddRange(systemMessage, firstMessage);
}
var newMessage = new CharacterChatHistory { Name = "user", Message = message };
db.ChatHistories.Add(newMessage);
```

⚠️ **Greeting `firstMessage` записывается в БД при первом обращении**, но НЕ при `/character spawn`. То есть когда персонаж только что появился и юзер видит «приветствие» от webhook'а — это ещё **не в истории**. История начинается с первого реального обращения к нему. Это значит, что свой собственный greeting AI «не помнит».

**Параметры генерации — каскад per-character / per-integration**:
```csharp
var settings = new GenerationSettings {
    Temperature       = (float)(orSpawnedCharacter.OpenRouterTemperature ?? orIntegration.OpenRouterTemperature)!,
    TopP              = (float)(orSpawnedCharacter.OpenRouterTopP        ?? orIntegration.OpenRouterTopP)!,
    // ... все 9 параметров через тот же ?? паттерн
};
var model = orSpawnedCharacter.OpenRouterModel ?? orIntegration.OpenRouterModel!;
```

**Retry-цикл**:
```csharp
int attempt = 0;
while (attempt < 3) {
    try {
        response = await _client.CompleteAsync(...);
    } catch (JsonReaderException e) {
        _logger.Error(e, "Could not parse OpenRouter API response");
        continue;   // ⚠ attempt не инкрементится!
    }
    var characterResponse = response.Choices.FirstOrDefault(...)?.Message?.Content;
    if (characterResponse is null) { attempt++; continue; }
    // ... успех: сохраняем "assistant" в БД, возвращаем
}
throw new ChatModuleException("Failed to get response from OpenRouter");
```

⚠️ **Bug-suspect**: при `JsonReaderException` `attempt` **не инкрементируется**, значит при стабильной поломке парсинга — бесконечный цикл. На практике вряд ли случается (OpenRouter обычно отвечает валидным JSON), но это потенциальная hang-точка.

⚠️ **Постпроцессинг ответа**: `characterResponse.Replace($"{orSpawnedCharacter.CharacterName}:", string.Empty, ignoreCase)` — снимает префикс «Имя_персонажа:» если LLM добавил. Лёгкий хак для post-cleanup.

**Контракт исключений**: бросает только `ChatModuleException`. `JsonReaderException` глушится (с лог-записью). Остальные исключения от `_client.CompleteAsync` пробрасываются.

**Уникальное среди модулей**:
- Открывает собственный DbContext (`new AppDbContext(...)` на каждый вызов) — единственный модуль, лезущий в БД.
- Имеет конструктор с параметрами.
- Имеет retry-логику.
- Бросает `ChatModuleException`.
- Кастит к concrete-классу `OpenRouterSpawnedCharacter`.
- Выполняет `.Replace($"{name}:", ...)` post-cleanup.
- `IsChatOnly` (см. `OpenRouterGuildIntegration.IsChatOnly => true`).

---

### 4.4 `ChubAiModule` (`Modules/Modules/Search/ChubAiModule.cs`)

**Реализует**: только `ISearchModule`. **Не имеет chat**.

**Использует из (in-tree) клиента**:
- `_client.SearchAsync(query, NsfwMode) → ChubAiCharacter[]`
- `_client.GetCharacterInfoAsync(fullPath) → ChubAiCharacter`

**Особенность сигнатуры `GetCharacterInfoAsync`**: для Chub `characterId` — это `fullPath` вида `"author/character-name"`, не GUID. Это работает потому, что `CommonCharacter.CharacterId` — это `string`, и каждый провайдер может класть туда что хочет. Но при рефакторинге надо помнить: ID-формат разный по провайдерам.

**`IIntegration` параметр игнорируется** в `SearchAsync(query, allowNsfw, integration)` — `Chub` публичный, креды не нужны. Параметр сохранён для соответствия интерфейсу `ISearchModule`. В `SearchAsync` он используется только для `integrationType` (передаётся в адаптер), но **не для аутентификации**:
```csharp
public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IIntegration integration) {
    var characters       = await _client.SearchAsync(query, ...);
    var integrationType  = integration.GetIntegrationType();   // нужно адаптеру для metadata
    return characters.Select(sc => new ChubCharacterAdapter(sc, integrationType).ToCommonCharacter()).ToList();
}
```

⚠️ Это значит: при поиске Chub-каталога для OpenRouter-интеграции, `integration.GetIntegrationType() == OpenRouter`, и адаптер получит **`IntegrationType.OpenRouter`** — это правильно (мы хотим, чтобы найденный character был привязан к OpenRouter-вызову, а не к ChubAI), но логика неочевидна и зависит от `ExtensionMethods.GetIntegrationType(IIntegration)`.

**Контракт исключений**: ChubAi-клиент имеет свой набор исключений (`Modules/Clients/ChubAiClient/Exceptions/`). Они **не классифицированы** как UserFriendly в `CommonHelper.ValidateUserFriendlyException`. То есть при сетевой ошибке Chub юзер увидит «Something went wrong» с trace-id, а не текстом ошибки. Кандидат на расширение классификатора.

---

### 4.5 `ModuleBase<TClient>` — общий базовый класс

```csharp
public abstract class ModuleBase<TClient> where TClient : new() {
    protected readonly TClient _client = new();
}
```

Делает ровно одно: ограничивает `TClient` default-конструктором и инициализирует поле. Это значит:

1. **Все upstream-клиенты должны иметь `()` ctor**. Если когда-нибудь `OpenRouterClient` потребует API-key в ctor — конструкция через `new TClient()` сломается.
2. **Параметризовать клиент через ctor нельзя** — `OpenRouterModule` это обходит, передавая параметры в собственные поля и игнорируя конструкцию `_client`.
3. **Generic параметр в основном декоративный**. Полезен только тем, что в IDE по типу видно «этот модуль работает с CharacterAiClient».

**Кандидат на упрощение**: убрать `ModuleBase<TClient>` совсем, держать клиент явно в каждом модуле. Это не ломает контракты (никто не использует `ModuleBase<TClient>` как точку расширения), и снимает ограничение default-ctor.

---

## 5. Адаптеры — мост между upstream-DTO и `CommonCharacter`

### 5.1 Иерархия

```
ICharacterAdapter  (Shared.Abstractions.Adapters)
   ▲
CharacterAdapterBase<T>  (abstract, Modules/Abstractions/Base)
   ├── CharacterAdapter<T>
   │      └── CaiCharacterAdapter : CharacterAdapter<CaiCharacter>
   │
   └── AdoptableCharacterAdapter<T>  (impl IAdoptableCharacterAdapter)
          ├── SakuraCharacterAdapter  : ...<SakuraCharacter>
          └── ChubCharacterAdapter    : ...<ChubAiCharacter>
```

### 5.2 Контракт `ICharacterAdapter`

```csharp
public interface ICharacterAdapter {
    CommonCharacter ToCommonCharacter();
    TResult         GetCharacter<TResult>();   // ⚠ обратное преобразование
    string          GetCharacterName();
    string          GetCharacterLink();
    string          GetAuthorLink();
    string          GetCharacterDescription();
    IntegrationType GetIntegrationType();
}

public interface IAdoptableCharacterAdapter : ICharacterAdapter {
    CharacterSourceType GetCharacterSourceType();
    string              GetCharacterDefinition();   // дополнительно у adoptable
}
```

**Три направления преобразования**:

1. **Upstream-DTO → `CommonCharacter`** (`ToCommonCharacter`) — для отображения в search-результатах и embed-карточках.
2. **`ICharacterAdapter` → upstream-DTO** (`GetCharacter<TResult>`) — для конструкторов `*SpawnedCharacter`, которым нужны исходные поля для копирования в EF-сущность.
3. **Upstream-DTO → отображаемый текст** (`GetCharacterDescription`/`GetCharacterDefinition`) — через `Modules/Helpers/Templates.cs`.

### 5.3 `GetCharacter<TResult>()` — обратное преобразование

```csharp
// CharacterAdapterBase.cs
TResult ICharacterAdapter.GetCharacter<TResult>()
    => (TResult)Convert.ChangeType(Character, typeof(TResult))!;
```

⚠️ **`Convert.ChangeType` для DTO** — странный выбор. Для совместимых типов (когда `TResult` равен `T`) это no-op cast. Для несовместимых — упадёт `InvalidCastException`. Кандидат на замену:

```csharp
TResult ICharacterAdapter.GetCharacter<TResult>() => (TResult)(object)Character!;
```

**Где используется**: только в трёх местах — конструкторы `SakuraAiSpawnedCharacter(IAdoptableCharacterAdapter)`, `CaiSpawnedCharacter(ICharacterAdapter)`, и **специально** для извлечения upstream-DTO. Например, `SakuraAiSpawnedCharacter`:

```csharp
public SakuraAiSpawnedCharacter(IAdoptableCharacterAdapter adapter) {
    var sakuraCharacter = adapter.GetCharacter<SakuraCharacter>();
    SakuraMessagesCount = sakuraCharacter.messageCount;
    SakuraScenario      = sakuraCharacter.scenario;
    // ...
}
```

То есть adapter работает в две стороны: `ToCommonCharacter` для поиска и UI, `GetCharacter<T>` для конструкторов EF-сущностей.

### 5.4 Адаптеры по одному

| Адаптер | Wraps | Особенности |
|---|---|---|
| `CaiCharacterAdapter` | `CaiCharacter` | Не adoptable. Включает hard-coded URL для аватарки: `"https://characterai.io/i/200/static/avatars/{avatar_file_name}"`. `IsNfsw = false` всегда. `CharacterStat = participant__num_interactions.ToString()`. |
| `SakuraCharacterAdapter` | `SakuraCharacter` | Adoptable. Description строит через `Templates.BuildCharacterDescription` с теглайном `"[ tag1, tag2, ... ]"`. Definition — через `Templates.BuildCharacterDefinition` с `Character.persona`, `Character.scenario`, `exampleConversation` mapping role/content. `CharacterStat = messageCount`. |
| `ChubCharacterAdapter` | `ChubAiCharacter` | Adoptable. `CharacterId = fullPath` (например `"alice/maid"`). Author извлекается как `fullPath.Split('/').First()`. Если нет first-message — fallback `"*{Name} has joined the server*"`. Definition: `Character.Definition.Tavern_personality + Personality + Scenario + Example_dialogs`. Принимает `IntegrationType` как ctor-параметр (см. §4.4). |

### 5.5 Нет `OpenRouterCharacterAdapter`

Это **специально**. У OpenRouter нет своего каталога — он использует адаптеры от Sakura/Chub. То есть путь:

```
Sakura search   → SakuraCharacterAdapter   → AdoptableCharacterAdapter
                                            → передаётся в OpenRouterSpawnedCharacter ctor
                                            → копируются adopted-поля
Chub search     → ChubCharacterAdapter     → то же самое
```

Это объясняет, почему `ChubCharacterAdapter` **может** вернуть `IntegrationType.OpenRouter` (если каталог Chub использовался для OpenRouter-интеграции) — параметр в ctor-е специально для этого.

---

## 6. `Templates` — формат для апстрима

`Modules/Helpers/Templates.cs` (`~96 LOC`) — это **stateless-набор статических билдеров**, превращающий поля upstream-DTO в готовый текст. Их два:

### 6.1 `BuildCharacterDescription(name, tagline?, description, scenario?)`

Используется для **embed-карточки в Discord** (когда юзер делает `/character info`):

```
**{Name}**
{tagline (если есть)}

{description (первые 16 строк)}

**Scenario**
{scenario (если есть и не начинается с "{{char}} is")}
```

Скип `"{{char}} is..."` — потому что некоторые DTO кладут плейсхолдер в scenario (он бесполезен для embed-карточки в чистом виде).

### 6.2 `BuildCharacterDefinition(name, personality, scenario?, exampleDialog?)`

Используется как **system-prompt контекст для LLM** (вшивается в OpenRouter):

```
About {Name}:
[PERSONALITY]
{personality}
[PERSONALITY_END]

[EXAMPLE_DIALOG]
{User: ... \n {Name}: ... \n ... }
[EXAMPLE_DIALOG_END]

[SCENARIO]
{scenario}
[SCENARIO_END]
```

После сборки прогоняется через `FillCharacterPlaceholders(name)` — заменяются `{{CHAR}}`, `{{BOT}}`, `<CHAR>`, `<BOT>` на реальное имя.

⚠️ Маркеры `[PERSONALITY]`/`[EXAMPLE_DIALOG]`/`[SCENARIO]` — это **жёсткий контракт с прошлой версией prompt-инжиниринга**. Если потребуется сменить формат под новую модель LLM (например, ChatML или что-то другое) — это место правки.

### 6.3 `FillCharacterPlaceholders` / `FillUserPlaceholders` (extensions)

```csharp
"{{CHAR}}" "{{BOT}}" "<CHAR>" "<BOT>"  → имя персонажа
"{{user}}" "<user>"                      → mention юзера
```

`FillCharacterPlaceholders` вызывается в нескольких местах:
- `Templates.BuildCharacterDefinition` (внутри).
- `OpenRouterModule.CallCharacterAsync` — для system-prompt'а перед отправкой.
- `ActiveCharacterDecorator.SendGreetingAsync` — для first-message при greeting.

`FillUserPlaceholders` вызывается:
- `ActiveCharacterDecorator.SendMessageAsync` — для исходящих ответов персонажа.

⚠️ **Регистро-нечувствительно** (`StringComparison.InvariantCultureIgnoreCase`) — то есть `{{Char}}`, `{{CHAR}}`, `{{char}}` все ловятся. Это совместимо с разными upstream-форматами (CAI-style, SillyTavern-style и т.д.).

---

## 7. Где хранится «full» character data — и зачем двойной запрос

При spawn'е пути такие:

```
1. /character spawn type:OpenRouter searchQuery:"alice"
   └─ CharacterCommands.SpawnCharacter
      └─ searchModule.SearchAsync(query, ...)            ← upstream call #1
         → List<CommonCharacter>  (lite-version, для UI)
      └─ кэш в ActiveSearchQueries

2. Юзер кликает кнопку Select
   └─ ButtonsHandler.UpdateSearchQueryAsync (action="select")
      └─ _integrationsMaster.SpawnCharacterAsync(channelId, selectedCharacter, guildIntegration)
         (передаём CommonCharacter из шага 1)
         └─ InteractionsHelper.CreateDiscordWebhookAsync(...)
         └─ _charactersDbRepository.CreateSpawnedCharacterAsync(commonCharacter, webhook, guildIntegration)
            ├─ searchModule.GetCharacterInfoAsync(commonCharacter.CharacterId, integration)  ← upstream call #2 (!)
            │  → ICharacterAdapter (full-version)
            │  → fullCommonCharacter = adapter.ToCommonCharacter()
            ├─ NEW *SpawnedCharacter(adapter, integration)  ← copy из adapter (а не commonCharacter)
            └─ перезаписывает поля из fullCommonCharacter поверх
```

⚠️ **Двойной upstream-запрос**:
- Search возвращает **lite-version** `CommonCharacter` (без Definition, Scenario, exampleDialog) — её хватает для UI-карточки.
- Spawn требует **full-version** — поэтому вызывает `GetCharacterInfoAsync` ещё раз.

Это **сознательное архитектурное решение**: при поиске может вернуться 30 character'ов, и тащить full-version для всех — расточительно. Но при единичном спавне — лишний round-trip. Кандидат на оптимизацию: кэшировать `ICharacterAdapter` в `ActiveSearchQueries` после успешного `Select` (или сразу при первом раскрытии full-info).

⚠️ **Имя персонажа задаётся дважды**:
1. В ctor `*SpawnedCharacter(adapter, ...)` — `CharacterName = adapter.GetCharacterName()`.
2. В `CharactersDbRepository.CreateSpawnedCharacterAsync:242` — `newSpawnedCharacter.CharacterName = fullCommonCharacter.CharacterName`.

Сейчас оба значения идентичны (тот же adapter), так что баг не виден. Но если в будущем добавить кэширование/трансформацию между шагами — можно поймать рассинхрон.

### 7.1 CallPrefix — генерация

`CharactersDbRepository.CreateSpawnedCharacterAsync:182-204`:

```csharp
var characterName = FILTER_REGEX.Replace(commonCharacter.CharacterName.Trim(), "");  // [^a-zA-Z0-9\s]
if (characterName.Contains(' ')) {
    var split = characterName.Split(' ', ...);
    callPrefix = $"@{split[0][0]}{split[1][0]}";    // "Alice Wonder" → "@AW"
} else if (characterName.Length > 2) {
    callPrefix = $"@{characterName[..2]}";          // "Maid" → "@Ma"
} else {
    callPrefix = $"@{characterName}";               // "Al" → "@Al"
}
// ↓ потом
newSpawnedCharacter.CallPrefix = callPrefix.ToLower();   // "@aw"
```

⚠️ Не проверяется коллизия с уже существующими persons в канале. Если уже есть `@aw` (от предыдущей `Alice Wonder`) — новый персонаж получит **тот же** `@aw`, и cached storage найдёт обоих по этому префиксу через `FirstOrDefault`. Кандидат на bug.

### 7.2 Дефолты — hardcoded в Repository

```csharp
newSpawnedCharacter.ResponseDelay        = 3;
newSpawnedCharacter.FreewillFactor       = 3;
newSpawnedCharacter.EnableSwipes         = false;
newSpawnedCharacter.FreewillContextSize  = 3000;
newSpawnedCharacter.EnableQuotes         = false;
newSpawnedCharacter.EnableStopButton     = true;
newSpawnedCharacter.SkipNextBotMessage   = false;
```

Эти 7 числовых/булевых дефолтов **захардкожены** в коннекторном слое (`CharactersDbRepository`). Не в `BotConfig`, не в БД. Кандидат на вынос в config.

---

## 8. Сквозные трассировки

Каждая трассировка — последовательность вызовов от Discord-события до upstream API и обратно. Стрелка `↓` — синхронный путь, `↻` — мутации, `⚠` — точки риска.

### 8.1 Search — найти персонажа в каталоге

Триггер: `/character spawn type:Sakura searchQuery:"alice"` без `characterId`.

```
SocketSlashCommand
  ↓
SlashCommandsHandler.HandleSlashCommand
  ↓
InteractionService.ExecuteCommandAsync
  ↓
CharacterCommands.SpawnCharacter (annotated [SlashCommand])
  ↓ NSFW-проверка, лимит вебхуков ≤14, есть ли integration
  ↓ выбор searchModule:
  ↓   if !integration.IsChatOnly: GetSearchModule(integrationType)
  ↓   else if useCatalog: GetSearchModule(sourceType)        ⚠ упадёт если нет useCatalog для OR
  ↓
searchModule.SearchAsync(query, showNsfw, integration)
  │
  ├─ SakuraAiModule.SearchAsync:
  │     _client.SearchAsync(query, allowNsfw)               ← upstream HTTP
  │     → SakuraCharacter[]
  │     → .Select(sc => new SakuraCharacterAdapter(sc).ToCommonCharacter())
  │
  ├─ CaiModule.SearchAsync:
  │     _client.SearchAsync(query, integration.CaiAuthToken) ← upstream HTTP (⚠ нужен токен)
  │     → CaiCharacter[]
  │     → .Select(c => new CaiCharacterAdapter(c).ToCommonCharacter())
  │
  └─ ChubAiModule.SearchAsync:
        _client.SearchAsync(query, NsfwMode.allowNSFW|noNSFW)
        → ChubAiCharacter[]
        → .Select(c => new ChubCharacterAdapter(c, integration.GetIntegrationType()).ToCommonCharacter())

  ↓
List<CommonCharacter>  (lite, без Definition)
  ↓
SearchQuery (record, кешируется в ActiveSearchQueriesStorage по messageId)
  ↓
embed с пагинацией + кнопки "sq~sep~up|down|left|right|select"
```

### 8.2 Select → Spawn

Триггер: юзер кликнул кнопку «✅ select» или передал `characterId:` сразу.

```
SocketMessageComponent.ButtonExecuted
  ↓
ButtonsHandler.UpdateSearchQueryAsync (action="select")
  ↓ ValidateChannelPermissionsAsync
  ↓
IntegrationsMaster.SpawnCharacterAsync(channelId, selectedCharacter, guildIntegration)
  ├─ InteractionsHelper.CreateDiscordWebhookAsync(channel, name, imageUrl)
  │    │  ├─ fix "discord" → "disсord" (Cyrillic O)
  │    │  ├─ download avatar (CommonHelper.DownloadFileAsync)
  │    │  ├─ if size > 10MB: MagicScaler resize → 600px JPEG
  │    │  └─ channel.CreateWebhookAsync(name, stream)        ← Discord API
  │    └─ → IWebhook { Id, Token, ChannelId }
  ├─ new DiscordWebhookClient(webhook.Id, webhook.Token)     ← Discord API (RPC verify)
  ├─ CachedWebhookClients.Add(webhookId, webhookClient)
  ├─ CharactersDbRepository.CreateSpawnedCharacterAsync(commonCharacter, webhook, guildIntegration)
  │    ├─ FILTER_REGEX → cleanName, generate callPrefix (@AW / @Ma / ...)
  │    ├─ searchModule.GetCharacterInfoAsync(commonCharacter.CharacterId, integration) ⚠ upstream call #2
  │    │   → ICharacterAdapter (full-version)
  │    ├─ switch on integrationType:
  │    │     Sakura:     new SakuraAiSpawnedCharacter((IAdoptableCharacterAdapter)adapter)
  │    │     CAI:        new CaiSpawnedCharacter(adapter)
  │    │     OpenRouter: new OpenRouterSpawnedCharacter((IAdoptableCharacterAdapter)adapter, (IOpenRouterIntegration)integration)
  │    ├─ перезапись CharacterId, Name, FirstMessage, ImageLink, Author, IsNfsw из fullCommonCharacter
  │    ├─ операционные поля: WebhookId, WebhookToken, CallPrefix, DiscordChannelId
  │    ├─ hardcoded дефолты: ResponseDelay=3, FreewillFactor=3, ...
  │    └─ db.SaveChangesAsync                                 ← INSERT в *SpawnedCharacters
  ├─ CachedCharacters.Add(newSpawnedCharacter)
  ├─ MetricsWriter.Write(MetricType.CharacterSpawned, ...)
  └─ → ISpawnedCharacter
  ↓
ButtonsHandler сразу:
  ↓ new ActiveCharacterDecorator(spawnedCharacter, webhookClient)
  ↓ activeCharacter.SendGreetingAsync(userMention, threadId?)
       │
       └─ если CharacterFirstMessage есть:
            ├─ FillCharacterPlaceholders(name)               ← "{{CHAR}}" → "Alice"
            ├─ FillUserPlaceholders(userMention)             ← "{{user}}" → "<@123>"
            └─ webhookClient.SendMessageAsync(text, threadId?)  ← Discord API
              (если text > 2000: split + thread "MESSAGE LENGTH LIMIT EXCEEDED")
```

⚠️ **Greeting НЕ записывается в `CharacterChatHistory`** для OpenRouter. То есть когда первое реальное сообщение пользователя приходит, OpenRouter не «помнит» свой собственный приветственный пост — этого приветствия нет в истории. Видно это в `OpenRouterModule.CallCharacterAsync` (`OpenRouterModule.cs:62-72`): assistant-message с greeting пишется в БД **только при первом обращении к персонажу**, причём не текущее сообщение пользователя, а тот же greeting, который webhook уже опубликовал в Discord.

### 8.3 Chat — каждое сообщение

Триггер: любое guild-сообщение, прошедшее `WatchDog.ValidateUser`.

```
Discord.MessageReceived
  ↓
MessagesHandler.HandleMessage  (Task.Run)
  ↓
HandleMessageAsync:
  ↓ skip self / ~ignore / non-text-channel
  ↓ load CachedCharacters[channel] (filter c.WebhookId.StartsWith(authorId))
  ↓ четыре триггера в параллель:
  ↓   FindCharacterByReplyAsync   → CallCharacterAsync (direct)
  ↓   FindCharacterByPrefixAsync  → CallCharacterAsync (direct)
  ↓   FindRandomCharacterAsync    → CallCharacterAsync (indirect)
  ↓   FindHunterCharactersAsync   → CallCharacterAsync (direct, x N)
  ↓
CallCharacterAsync(spawnedCharacter, msg, isIndirect):
  ↓ NSFW проверка
  ↓ semaphore: IntegrationsDbRepository.GetGuildIntegrationAsync(spawnedCharacter)
  ↓ cachedCharacter.QueueAddCaller (FIFO max 5)
  ↓ wait turn (poll каждые 500ms, max 2min)
  ↓ semaphore: CharactersDbRepository.GetSpawnedCharacterByIdAsync(...) (force-reload!)
  ↓ delay: max(5, ResponseDelay) если автор bot/webhook, иначе ResponseDelay
  ↓ messagesFormat lookup: cascade char → channel → guild → BotConfig.DEFAULT
  ↓
если isIndirect && FreewillContextSize > 0:
  ↓ channel.GetMessagesAsync(20).FlattenAsync()    ← Discord API
  ↓ собираем строки до FreewillContextSize символов
  ↓ userMessage = многострочный контекст
иначе:
  ↓ userMessage = ReformatUserMessage(socketUserMessage, callPrefix, messageFormat)
       (Reformat применяет MessagesHelper.BringMessageToFormat — placeholder substitute)

  ↓
IntegrationsHub.GetChatModule(integrationType).CallCharacterAsync(spawned, integration, userMessage):
  │
  ├─ SakuraAiModule:
  │    cast to (ISakuraCharacter, ISakuraIntegration)
  │    if SakuraChatId is null:
  │       _client.CreateNewChatAsync(sessionId, refresh, sakuraCharacter, msg)
  │       → SakuraChat { chatId, messages[] }
  │       response = messages.Last().content
  │       sakuraCharacter.SakuraChatId = chatId          ↻ мутация EF-сущности
  │    else:
  │       _client.SendMessageToChatAsync(sessionId, refresh, chatId, msg)
  │       → SakuraMessage { content }
  │       response = content
  │    return new CommonCharacterMessage { Content = response }
  │
  ├─ CaiModule:
  │    cast to (ICaiCharacter, ICaiIntegration)
  │    if CaiChatId is null:
  │       caiCharacter.CaiChatId = _client.CreateNewChat(...)   ⚠ sync!
  │    var data = new CaiSendMessageInputData { ... CaiChatId, AuthToken, ... }
  │    var response = _client.SendMessageToChat(data)            ⚠ sync!
  │    return Task.FromResult(new CommonCharacterMessage { Content = response })
  │
  └─ OpenRouterModule:
       cast to (OpenRouterSpawnedCharacter, IOpenRouterIntegration)
       open new AppDbContext(_connectionString)
       history = db.ChatHistories.Where(SpawnedCharacterId == id).ToList()
       if history.Count == 0:
          add system-message: prompt = (charPrompt ?? intPrompt ?? defaultPrompt).FillPlaceholders(name) + "\n" + AdoptedCharacterDefinition
          add assistant-message: CharacterFirstMessage
       add user-message: msg
       map history → ChatMessage[] (Role, Content)
       settings = GenerationSettings(temp/topP/topK/.../maxTokens — каскад char ?? integration)
       model = char.OpenRouterModel ?? integration.OpenRouterModel
       attempt = 0
       while attempt < 3:
           try { response = await _client.CompleteAsync(apiKey, model, messages, settings) }
           catch JsonReaderException { continue; }      ⚠ attempt++ забыт!
           if response.Choices.None: attempt++; continue
           characterResponse = ...Replace($"{name}:", "")  (cleanup)
           db.ChatHistories.Add(assistant-message)
           db.SaveChangesAsync()
           return new CommonCharacterMessage { Content = characterResponse }
       throw new ChatModuleException("Failed to get response from OpenRouter")

  ↓ CommonCharacterMessage { Content }
  ↓
ActiveCharacterDecorator.SendMessageAsync(content, userMention, threadId?)
  ↓ FillUserPlaceholders(userMention)
  ↓ webhookClient.SendMessageAsync(text, threadId?)        ← Discord API
       (если > 2000 chars: chunk + thread)
  ↓ → ulong messageId

  ↓
обновление spawnedCharacter:
  ↓ LastCallerDiscordUserId, LastDiscordMessageId, LastCallTime, MessagesSent++
  ↓ semaphore: UpdateSpawnedCharacterAsync (сохраняет SakuraChatId/CaiChatId если был mутирован!)
  ↓ MetricsWriter.Write(MetricType.CharacterCalled)
```

⚠️ **Особый момент с мутацией chatId**: `SakuraAiModule`/`CaiModule` мутируют переданный character. Этот character — это **EF tracking entity** (потому что он был получен через `GetSpawnedCharacterByIdAsync`, не `AsNoTracking`). Поэтому `UpdateSpawnedCharacterAsync` сохраняет мутацию в БД через стандартный change-tracking + `Update(entity)`. Но эта последовательность завязана на «один DbContext = один поток» и ломается если кто-то перенесёт мутацию вне семафора.

### 8.4 Reset — стереть контекст

Триггер: `/character reset @ai`.

```
CharacterCommands.ResetCharacter
  ↓ FindCharacterAsync
  ↓ switch (spawnedCharacter):
  ├─ IAdoptedCharacter (т.е. OpenRouter):
  │    db.ChatHistories.RemoveRange(where == id)
  │    db.SaveChangesAsync                ← удалили всё, в т.ч. system-prompt + greeting + history
  ├─ CaiSpawnedCharacter:
  │    caiSpawnedCharacter.CaiChatId = null    ← апстрим продолжает чат, но мы «забыли» chatId
  └─ SakuraAiSpawnedCharacter:
       sakuraAiSpawnedCharacter.SakuraChatId = null   ← то же
  ↓ UpdateSpawnedCharacterAsync
  ↓ ActiveCharacterDecorator.SendGreetingAsync (постит first-message заново)
  ↓ cachedCharacter.WideContextLastMessageId = greetingMessageId
```

⚠️ **CAI/Sakura — utf-разница с OpenRouter**: для них «reset» это потеря _ссылки_ на upstream-чат. Сам чат на стороне платформы остаётся. Если бот когда-нибудь поддержит «восстановление» (привязка к старому chatId через `/character edit chat-id`), то можно вернуться. Для OpenRouter — данные стёрты безвозвратно.

⚠️ **Нет очистки `WideContextLastMessageId`** для OpenRouter — он только обновляется на новое значение greeting. Если до reset'а был накоплен «Wide context» (контекст freewill-вызовов), его **не сбрасывают** явно. Минорный баг.

### 8.5 Auth — Sakura

Триггер: `/integration create type:SakuraAI` → модалка с email.

```
ModalsHandler.CreateSakuraAiIntegrationAsync
  ↓
IntegrationsMaster.SendSakuraAiMailAsync(modal, email)
  ↓ IntegrationsHub.SakuraAiModule.SendLoginEmailAsync(email)
       └─ _client.SendLoginEmailAsync(email)      ← upstream HTTP, отдаёт SakuraSignInAttempt
  ↓ ответ юзеру: «check your inbox, click confirmation in Incognito»
  ↓ data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, channelId, userId)
       └─ "{channelId}~{userId}~{Id}~{Email}~{Cookie};"   ⚠ кустарная сериализация через ~
  ↓ db.StoredActions.Add(new StoredAction(SakuraAiEnsureLogin, data, maxAttemtps:25))
  ↓ db.SaveChangesAsync

[фоновый таймер каждые 20с — BackgroundWorker.RunStoredActions]:

action.Attempt++
job = IntegrationsMaster.EnsureSakuraAiLoginAsync(action):
  ↓ signInAttempt = action.ExtractSakuraAiLoginData() (split по ~)
  ↓ result = await IntegrationsHub.SakuraAiModule.EnsureLoginByEmailAsync(signInAttempt)
       └─ _client.EnsureLoginByEmailAsync(...)    ← upstream HTTP
            если юзер ещё не подтвердил → бросает SakuraException → action.Status = Pending → ретрай
            если подтвердил → SakuraAuthorizedUser{Username, UserImageUrl, SessionId, RefreshToken}
  ↓ найти или создать SakuraAiGuildIntegration в БД
       SakuraEmail        = signInAttempt.Email
       SakuraSessionId    = result.SessionId
       SakuraRefreshToken = result.RefreshToken
  ↓ db.SaveChangesAsync
  ↓ MetricsWriter.Write(IntegrationCreated)
  ↓ ITextChannel.SendMessageAsync(user.Mention, "SakuraAI authorized")  ← Discord API
  ↓ action.Status = Finished
```

### 8.6 Auth — CAI

```
ModalsHandler.CreateCharacterAiIntegrationAsync
  ↓
IntegrationsMaster.SendCharacterAiMailAsync(interaction, email)
  ↓ IntegrationsHub.CharacterAiModule.SendLoginEmailAsync(email)
       └─ _client.SendLoginEmailAsync(email)     ← upstream HTTP (no return value)
  ↓ ответ юзеру: «копируй link из письма НЕ КЛИКАЯ, вставь в /integration confirm data:<url>»

[пользователь вручную]: /integration confirm type:CharacterAI data:https://character.ai/login/xxx

IntegrationManagementCommands.Confirm
  ↓ caiUser = IntegrationsHub.CharacterAiModule.LoginByLinkAsync(data)
       └─ _client.LoginByLinkAsync(link) → AuthorizedUser{Token, UserId, Username, UserEmail, UserImageUrl}
  ↓ new CaiGuildIntegration с полями из caiUser
  ↓ db.SaveChangesAsync
  ↓ MetricsWriter.Write(IntegrationCreated)
  ↓ ответ юзеру с эмбедом
```

### 8.7 Auth — OpenRouter

```
ModalsHandler.CreateOpenRouterIntegrationAsync
  ↓ apiKey = modal.input("api-key").Trim()
  ↓ if !apiKey.StartsWith("sk-or"): throw UserFriendlyException
  ↓ model = modal.input("model").Trim()
  ↓ new OpenRouterGuildIntegration { OpenRouterApiKey, OpenRouterModel, DiscordGuildId, CreatedAt }
  ↓ db.SaveChangesAsync
  ↓ MetricsWriter.Write(IntegrationCreated)
  ↓ ответ юзеру
```

⚠️ **Никакой проверки валидности API-ключа на стороне OpenRouter** — бот примет любой `sk-or...` префикс. Реальный отказ юзер увидит только при первом `/character spawn` или первой попытке чата.

---

## 9. Сравнительная таблица провайдеров

| Аспект | SakuraAI | CharacterAI | OpenRouter | ChubAI |
|---|---|---|---|---|
| **Модуль** | `SakuraAiModule` | `CaiModule` | `OpenRouterModule` | `ChubAiModule` |
| **Реализует** | IChat + ISearch | IChat + ISearch | IChat | ISearch |
| **Adoptable?** | ✅ да (для OR) | ❌ нет | ❌ нет | ✅ да (для OR) |
| **Ctor параметризован** | ❌ | ❌ | ✅ (connStr, defaultPrompt) | ❌ |
| **Auth-флоу** | email + magic link + poll | email + paste link | API key (+ default model) | публичный, без auth |
| **Auth-state в БД** | SessionId + RefreshToken | AuthToken + UserId + Username | ApiKey + Model | — |
| **Storage chat-history** | upstream (SakuraChatId) | upstream (CaiChatId) | у нас (CharacterChatHistory таблица) | — |
| **Lazy-create chat** | ✅ при первом сообщении | ✅ при первом сообщении | ❌ (история всегда в БД) | — |
| **Мутация character** | ✅ записывает SakuraChatId | ✅ записывает CaiChatId | ❌ | — |
| **Sync upstream calls** | нет (всё async) | ⚠ CreateNewChat / SendMessageToChat — sync | нет | нет |
| **NSFW поддержка** | ✅ | ❌ (всегда false) | ✅ | ✅ (filter в search) |
| **Retry-логика** | ❌ | ❌ | ✅ (3 попытки на пустой Choice) | ❌ |
| **Бросает ChatModuleException** | ❌ | ❌ | ✅ | n/a |
| **Каскад настроек генерации** | ❌ | ❌ | ✅ char ?? integration | n/a |
| **Каскад system-prompt** | ❌ | ❌ | ✅ char ?? integration ?? default | n/a |
| **Cast в `CallCharacter`** | ISakuraCharacter / ISakuraIntegration | ICaiCharacter / ICaiIntegration | **OpenRouterSpawnedCharacter** (concrete!) / IOpenRouterIntegration | n/a |
| **Адаптер** | SakuraCharacterAdapter (Adoptable) | CaiCharacterAdapter (regular) | — (использует Sakura/Chub) | ChubCharacterAdapter (Adoptable) |
| **`CharacterId` формат** | upstream GUID | upstream GUID (`external_id`) | (хранится adapter'овский) | `"author/name"` fullPath |
| **`CharacterStat`** | messageCount | num_interactions | n/a | StarCount |
| **`IsChatOnly`** | false | false | **true** | n/a |
| **DI/state** | static singleton в IntegrationsHub | static singleton | static singleton | static singleton |

---

## 10. Хрупкие места коннекторного слоя

В дополнение к §10 архитектурной карты, **специфичные для коннекторов**:

1. **Слабая типизация на сигнатурах** (`ICharacter`, `IIntegration`). Cast-ошибка превращается в `InvalidCastException` в runtime. Кандидат на generic-параметризацию: `IChatModule<TChar, TIntegration>` с явным `where TChar : ICharacter, TIntegration : IIntegration`.

2. **`IntegrationsHub` — static с side-effects в init**. `OpenRouterModule` создаётся через `new(DatabaseHelper.DbConnectionString, BotConfig.DEFAULT_SYSTEM_PROMPT)` при первом обращении. Если `BotConfig` ещё не инициализирован (rare but possible) — словит NRE.

3. **`OpenRouterModule.CallCharacterAsync` retry без инкремента `attempt`** при `JsonReaderException` (см. §4.3). Потенциальный hang.

4. **Adapter `Convert.ChangeType`** (§5.3) — небезопасный приём, лучше прямой cast.

5. **Двойной upstream-запрос на spawn** (§7) — performance penalty.

6. **Generation параметры OpenRouter — non-null assert через `!`** (`OpenRouterModule.cs:94-103`):
   ```csharp
   Temperature = (float)(orSpawnedCharacter.OpenRouterTemperature ?? orIntegration.OpenRouterTemperature)!
   ```
   Если **обе** ссылки `null` (что не должно случаться, но теоретически возможно для созданных до миграции записей) — NRE. Кандидат на explicit fallback на `BotConfig`-defaults.

7. **`CallPrefix` коллизия** (§7.1) — два «Alice»-персонажа в одном канале получат `@al` оба, и `Find` найдёт первого попавшегося.

8. **`OpenRouterModule.CallCharacterAsync` открывает свой DbContext каждый вызов**, при этом `MessagesHandler` тоже держит свой `AppDbContext` (через DI). На одно сообщение — **два разных EF-контекста параллельно к одной БД**. Возможны мелкие race conditions при параллельных update-ах одной строки `*SpawnedCharacters`.

9. **`Templates.BuildCharacterDefinition` маркеры `[PERSONALITY]/[EXAMPLE_DIALOG]/[SCENARIO]`** — жёстко вшиты. Не конфигурируемы, не зависят от модели.

10. **Submodule-клиенты не пинятся к версиям** (см. ARCHITECTURE.md §3) — `git submodule update --init` без коммита может потащить неcompatible API.

11. **Greeting не попадает в `CharacterChatHistory`** при spawn'е OpenRouter (§8.2). LLM не знает свой собственный приветственный пост.

12. **Chub-исключения не классифицированы** как UserFriendly (§4.4) — юзер видит «something went wrong», а не реальный текст.

13. **`(OpenRouterSpawnedCharacter)character`** — concrete cast (§4.3) нарушает DIP. Нельзя протестировать модуль с in-memory mock'ом без реальной EF-сущности с валидным `Id`.

14. **`IIntegration` — пустой маркер** — никакой compile-time проверки на корректность пары (character, integration).

---

## 11. Рекомендуемый план рефакторинга коннекторного слоя

В порядке цены/выгоды.

### Этап A — изоляция тестов (1-2 дня, низкий риск)

A1. **Превратить `IntegrationsHub` в `IIntegrationsHub` интерфейс + DI-singleton.** Снимает блокер на тестирование `MessagesHandler.CallCharacterAsync` и `CharacterCommands.SpawnCharacter`.
   - 4 строки регистрации в `CharacterEngineBot.CongifureShard`.
   - Один интерфейс с тремя методами.
   - Все callsite'ы заменить `IntegrationsHub.GetChatModule(...)` → `_hub.GetChatModule(...)` через DI.

A2. **Разрешить `Convert.ChangeType` → прямой cast** в `CharacterAdapterBase.GetCharacter<TResult>()`. Один LOC.

A3. **`OpenRouterModule.attempt` инкрементировать также при `JsonReaderException`.** Один LOC.

### Этап B — генерализация контракта (2-3 дня, средний риск)

B1. **Generic `IChatModule<TCharacter, TIntegration>`** — компилятор сам ловит mismatched pairs.
```csharp
public interface IChatModule<TChar, TIntegration>
    where TChar : class, ICharacter
    where TIntegration : class, IIntegration
{
    Task<CommonCharacterMessage> CallCharacterAsync(TChar c, TIntegration i, string msg);
}
```
Каждый модуль получит конкретный generic instantiation. `IntegrationsHub.GetChatModule(IntegrationType)` нужно адаптировать — например, через non-generic facade-метод, кастящий внутри. Это сужает поверхность runtime-cast'ов до одного места.

B2. **`OpenRouterSpawnedCharacter` — заменить cast на интерфейсный**. Создать `IOpenRouterPersistedCharacter : IOpenRouterCharacter, IPersistedCharacter` с `Guid Id { get; }`. Снимает зависимость модуля на конкретный EF-класс.

B3. **Унифицировать retry-логику** через extension method или helper. У всех трёх chat-модулей разные contract'ы — выровнять.

### Этап C — устранение неявных мутаций (3-5 дней, средний риск)

C1. **Перестать мутировать переданный character в Sakura/CAI модулях.** Возвращать новый record-результат `(content, ChatId?)`. Caller сам решает, что сохранить. Это ломает текущий contract — нужно проверить все call-sites.

C2. **Greeting в `CharacterChatHistory` для OpenRouter** — записывать при spawn'е, а не при первом сообщении. Снимает «AI не помнит свой greeting».

C3. **Адаптеры — кэшировать после select.** Сейчас адаптер из search'а отбрасывается, и spawn делает повторный запрос. Сохранить адаптер в `ActiveSearchQueries.Characters[i].Adapter` — снимает doubled round-trip.

### Этап D — структурные изменения (1-2 недели, высокий риск)

D1. **Убрать `ModuleBase<TClient>`** — каждому модулю явно держать `_client` с правильной инициализацией.

D2. **Унификация трёх `*SpawnedCharacters` таблиц** (см. ARCHITECTURE.md §13.6).

D3. **Адаптер для OpenRouter** (или явный «AdoptedAdapter» wrapper) — снимает асимметрию (§5.5).

D4. **Stricter `IIntegration`** — добавить `IntegrationType Type { get; }` в маркер. Сейчас он определяется через `ExtensionMethods.GetIntegrationType` через ещё один runtime-switch.

---

## 12. Карта тестируемости коннекторного слоя

Дополнение к §11 архитектурной карты, **специфичное для коннекторов**.

### 12.1 ✅ Easy (без подсадок)

| Что | Что тестируем |
|---|---|
| `Templates.BuildCharacterDescription` | теглайн, обрезка по 16 строк, fallback "*No description*", scenario с `{{char}} is`-skip |
| `Templates.BuildCharacterDefinition` | маркеры PERSONALITY/EXAMPLE_DIALOG/SCENARIO, FillPlaceholders интеграция |
| `Templates.FillCharacterPlaceholders/FillUserPlaceholders` | все 4 формы placeholder'а, регистро-нечувствительность |
| `CaiCharacterAdapter.ToCommonCharacter` | mapping CaiCharacter → CommonCharacter, hard-coded URL для аватарки |
| `SakuraCharacterAdapter.ToCommonCharacter` | то же |
| `ChubCharacterAdapter.ToCommonCharacter` | mapping + извлечение author из fullPath, fallback firstMessage |
| `ChubCharacterAdapter` с разными `IntegrationType` | проверка, что адаптер уважает source-чтение |
| `*Adapter.GetCharacterDefinition`/`GetCharacterDescription` | соответствие шаблонам |
| `IntegrationsHelper.GetIntegrationType(ICharacter)` | switch по типу |
| `ExtensionMethods.GetIntegrationType(IIntegration)` | switch по типу |

→ ~30-40 тестов, **полностью изолированных от Discord/EF/HTTP**. Это первый эшелон.

### 12.2 🟡 Medium (после этапа A1)

После того как `IntegrationsHub` станет DI-сервисом:

| Что | Подсадка |
|---|---|
| `CharactersDbRepository.CreateSpawnedCharacterAsync` | InMemoryDb + mock `ISearchModule.GetCharacterInfoAsync` (возвращает заранее заготовленный adapter) |
| `MessagesHandler.CallCharacterAsync` (часть до upstream) | mock `IChatModule.CallCharacterAsync` |
| `IntegrationsMaster.SpawnCharacterAsync` | mock `IIntegrationsHub` + mock `IWebhook` |
| `OpenRouterModule.CallCharacterAsync` (логика истории + cascade) | InMemoryDb + mock `OpenRouterClient` |

### 12.3 🔴 Hard (после рефакторинга)

| Что | Что мешает |
|---|---|
| `SakuraAiModule`/`CaiModule` end-to-end | upstream-клиенты не за интерфейсом — нельзя замокать, не патча `_client` через рефлексию |
| Real auth flow | требует реальный email-сервер |

**Рекомендация**: для модулей нужно ввести интерфейсы upstream-клиентов (`ISakuraAiClient`, `ICharacterAiClient`, `IOpenRouterClient`, `IChubAiClient`) — желательно генерируемые из контрактов submodule'ов. Тогда модули можно будет тестировать с пристёгнутыми мок-клиентами без подъёма submodule.

---

## 13. Связанные точки в основном коде

Код за пределами `Modules/`, который трогает коннекторный слой:

| Файл / метод | Что делает |
|---|---|
| `App/Services/IntegrationsHub.cs` | static диспетчер, 4 singleton |
| `App/Helpers/Masters/IntegrationsMaster.cs` | orchestrator: `SpawnCharacterAsync`, `SendSakuraAiMailAsync`, `SendCharacterAiMailAsync`, `EnsureSakuraAiLoginAsync` |
| `App/Repositories/CharactersDbRepository.cs:CreateSpawnedCharacterAsync` | дёргает `searchModule.GetCharacterInfoAsync`, копирует поля, мапит в одну из трёх EF-таблиц |
| `App/Handlers/MessagesHandler.cs:CallCharacterAsync` | главный consumer `IChatModule.CallCharacterAsync` |
| `App/Handlers/SlashCommands/CharacterCommands.cs:SpawnCharacter` | главный consumer `ISearchModule.SearchAsync`/`GetCharacterInfoAsync`; вилка `IsChatOnly`/`useCatalog` |
| `App/Handlers/ButtonsHandler.cs:UpdateSearchQueryAsync` | consumer `IntegrationsMaster.SpawnCharacterAsync` через select-кнопку |
| `App/Handlers/ModalsHandler.cs:CreateIntegrationAsync` | consumer login-флоу (Sakura/CAI/OpenRouter) |
| `App/Handlers/SlashCommands/IntegrationManagementCommands.cs:Confirm` | consumer `LoginByLinkAsync` (только CAI) |
| `App/Helpers/IntegrationsHelper.cs` | вспомогательные switch'и: GetIcon/GetColor/GetServiceLink/CanNsfw |
| `App/Services/BackgroundWorker.cs:RunStoredActions` | consumer `EnsureSakuraAiLoginAsync` (поллинг) |

---

*Документ описывает коннекторный слой по состоянию рабочей ветки `claude/analyze-repo-setup-5iQNw` (May 2026). При существенных изменениях `Modules/` или `IntegrationsHub` — обновить.*

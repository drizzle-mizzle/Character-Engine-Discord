# Карта конкурентности Character Engine

> Этот документ — **«где в системе встречаются параллельные потоки и что их защищает»**. Раз бот по описанию имел тысячи серверов и нагрузку, конкурентность — главный риск-вектор для рефакторинга и тестирования.
>
> Связанные документы:
> - `docs/BUSINESS_LOGIC.md` — что бот делает.
> - `docs/ARCHITECTURE.md` — структура кода (концурентность затронута в §5 крупными мазками).
> - `docs/CONNECTORS.md` — слой коннекторов с побочными мутациями.
>
> Здесь — узкий фокус на потоки, гонки, deadlock-риски, и план миграции к более здоровой concurrency-модели.

---

## 0. Карта в одном абзаце

Все события Discord приходят через **gateway-thread Discord.Net** (по одному на shard) и в каждом обработчике немедленно убегают в **`Task.Run(async () => {...})`** — в thread-pool, fire-and-forget, без cancellation. На любом сообщении в канале параллельно стартуют до **N независимых вызовов персонажа** (по числу совпавших триггеров: reply / prefix / freewill / hunt). Состояние делится на **БД** (через `AppDbContext`, не thread-safe, по одному на handler) и **process-wide static dictionaries** (через классы-фасады `*Storage`, `WatchDog`, `CacheRepository`). Защита трёхслойная: `ConcurrentDictionary<,>` на низком уровне, точечный `lock(obj)` для сложных мутаций (queue, watched-user counter), `SemaphoreSlim(1,1)` для сериализации EF-вызовов на тот же `AppDbContext`. **Самое опасное место** — `MessagesHandler.CallCharacterAsync`: внутри `await _semaphoreSlim.WaitAsync()` стоит синхронный `_repo.Method().GetAwaiter().GetResult()`. Сейчас это работает только потому, что Discord.Net и Npgsql везде `ConfigureAwait(false)`. На отдельном shard'е процесс держит **синглтон-style state** (`WatchDog`, `BackgroundWorker`) с флагом `_running`, который **блокирует повторный запуск даже после reconnect** — потенциальный stuck-state.

---

## 1. Источники параллелизма

«Источник» = место, где появляется новый concurrent поток исполнения. Не путать с «местом синхронизации».

### 1.1 Discord.Net gateway threads (по 1 на shard)

`DiscordShardedClient` поднимает по одному websocket-соединению с Discord на shard. На каждое входящее событие gateway-thread поднимает **C# event** (`MessageReceived`, `ButtonExecuted`, `SlashCommandExecuted`, и т.д.). Discord.Net использует TCS-цепочку: если handler возвращает `Task` — gateway-thread дожидается его перед обработкой следующего события **на том же shard'е**.

**Это значит**: если в каком-то handler-е написать `await Task.Delay(10_000)` без `Task.Run` — gateway-thread зависнет на 10 секунд, и **все** последующие события на этом shard'е встанут в очередь. Чтобы этого не было, **каждый** handler в боте написан так:

```csharp
public Task HandleMessage(SocketMessage socketMessage) {
    Task.Run(async () => {
        try {
            await HandleMessageAsync(socketMessage);
        } catch (...) { ... }
    });
    return Task.CompletedTask;   // ← gateway-thread сразу свободен
}
```

То есть **gateway-thread освобождается за миллисекунды**, а реальная работа уходит в thread-pool.

⚠️ Цена этой схемы: **fire-and-forget**. Никакая ошибка не пробросится наверх, никто не дождётся завершения, нет CancellationToken'а. Все исключения должны быть пойманы внутри Task.Run — иначе они станут `UnobservedTaskException` (хук есть, но это уже последний эшелон).

### 1.2 `Task.Run` в каждом handler-е

7 точек входа выкидывают работу в thread-pool fire-and-forget:

| Handler | Файл / строка | Что происходит после `Task.Run` |
|---|---|---|
| `MessagesHandler.HandleMessage` | `MessagesHandler.cs:51-99` | Полный цикл диспетчинга 4-х триггеров |
| `ButtonsHandler.HandleButton` | `ButtonsHandler.cs:43-84` | Диспетчинг кнопок (сейчас только search-pagination) |
| `ModalsHandler.HandleModal` | `ModalsHandler.cs:50-71` | CreateIntegration / OpenRouterSettings |
| `SlashCommandsHandler.HandleSlashCommand` | `SlashCommandsHandler.cs:43-67` | Диспетчер на 3 пути (special/admin/InteractionService) |
| `InteractionsHandler.HandleInteraction` | `InteractionsHandler.cs:23-48` | Post-execute hook (error reporting) |
| `OnJoinedGuild` | `CharacterEngineBot.cs:114-125` | EnsureGuildCached + register `/start` + log |
| `OnLeftGuild` | `CharacterEngineBot.cs:131-150` | Mark `Joined=false` + log |

⚠️ **Все 7 — без cancellation**. Если процесс получит SIGTERM в момент, когда 50 character-call'ов в полёте — они не завершатся корректно: процесс просто умрёт.

### 1.3 Параллельные триггеры в одном сообщении

Одно сообщение в канале может вызвать **до N+M+1 параллельных вызовов персонажа**:

- Reply-trigger: 0 или 1 (один character).
- Prefix-trigger: 0 или 1.
- Freewill-trigger: 0 или 1 (если несколько случайно сработали — выбирается один).
- Hunter-trigger: **0..N** (N = количество персонажей в канале, охотящихся на автора).

Все эти задачи стартуют синхронно через `callTasks.Add(...)` в `MessagesHandler.HandleMessageAsync:130-161` и затем `Task.WaitAll(callTasks.ToArray())` в строке 170.

⚠️ **`Task.WaitAll` блокирует thread-pool thread** на всё время самого медленного вызова (5-15 секунд на upstream API). В async-контексте должно быть `Task.WhenAll`. Мелкая, но ощутимая потеря throughput при нагрузке.

⚠️ **Блокирующее ожидание под `Task.Run`**: gateway-thread свободен, но thread-pool thread занят целиком. На крупном сервере с активным freewill — пул может выдыхаться.

### 1.4 `Parallel.ForEachAsync` на старте

`CharacterEngineBot.CacheUsersAndCharacters` (`CharacterEngineBot.cs:199-240`) использует `Parallel.ForEachAsync` для:

- параллельной заливки всех `*SpawnedCharacters` в `CachedCharacters`;
- параллельной заливки всех `DiscordUsers` в `_cachedUsers`.

Степень параллелизма — по умолчанию (`MaxDegreeOfParallelism = ProcessorCount * 1`). Но операция per-item — `cacheRepository.CachedCharacters.Add(...)` — **только** мутация `ConcurrentDictionary`, без I/O. То есть это не «много потоков читают БД параллельно», а **один поток читает + распределяет в map** через несколько worker-thread'ов. Польза сомнительная: всё блокируется на главном `db.HuntedUsers.AsNoTracking().ToArrayAsync()`.

⚠️ **`Task.WaitAll(characters, users)` в строке 239** — то же самое блокирующее ожидание, на главном потоке (`Program.Main`-цепочке). Оно блокирует startup пока обе задачи не отработают. Без cancellation, без timeout.

### 1.5 BackgroundWorker — 4 независимых loop'а

`BackgroundWorker.RunInLoop(jobTask, duration, log)` стартует **бесконечный `Task.Run(async () => while(true) { ... })`** для каждого job'а. Все 4 живут независимо в thread-pool:

- `RunStoredActions` — каждые 20 сек.
- `MetricsReport` — каждый час.
- `RevalidateBlockedUsers` — каждую минуту.
- `ClearCache` — каждые 5 минут.

Два последних дёргают **те же** static-storages, что и hot-path. То есть concurrency между «hot reads/writes» и «periodic cleanup» — реальная.

⚠️ **Без cancellation, без graceful shutdown.** `Environment.Exit(0)` на admin-команде `/shutdown` останавливает процесс жёстко — недоставленные сообщения, незаконченные транзакции теряются.

### 1.6 Множественность шардов

`DiscordShardedClient` создаёт по одному `DiscordSocketClient` на shard. На каждое `ShardReady` event выделяется **отдельный `CharacterEngineBot`** со своим DI-контейнером (`_instatnces[shardId] = new ...`). То есть N gateway-threads + N DI-scopes + 1 общая static state.

В реальности сейчас бот, скорее всего, работает с 1-2 шардами (порог 2500 гильдий). Но архитектурно это N-way concurrency.

### 1.7 OpenRouterModule открывает свой DbContext

Уникальная среди модулей особенность (см. CONNECTORS.md §4.3): на каждый `CallCharacterAsync` `OpenRouterModule` делает `await using var db = new AppDbContext(_connectionString)`. То есть **per-message появляется ещё один EF-контекст параллельно к тому, что держит `MessagesHandler`**. Они смотрят на одну БД, но через два независимых tracker'а.

⚠️ Возможны read-write conflicts: `MessagesHandler` обновляет `MessagesSent++`, `LastCallTime`, `OpenRouterModule` параллельно дописывает `CharacterChatHistory` — на одну `*SpawnedCharacters` строку оба контекста не пишут одновременно, но сама **изоляция транзакций не настроена**. По умолчанию Postgres — `READ COMMITTED`, что для наших патернов работает, но **detect-and-retry на `DbUpdateConcurrencyException` есть только в `CharactersDbRepository.DeleteSpawnedCharacterAsync`/`DeleteSpawnedCharactersAsync`**. Везде ещё — last-writer-wins.

### 1.8 Несколько одновременных команд от одного юзера

Discord не сериализует interactions от одного юзера. Если пользователь быстро жмёт две кнопки или два slash-command'а — это два **параллельных** event'а на gateway. Бот не делает per-user serialization (кроме `WatchDog` rate-limit на 15/30s). То есть `/character edit` и `/character reset` от одного юзера на одного персонажа могут лезть в БД параллельно.

---

## 2. Инвентарь shared state

### 2.1 Process-wide static state

Все эти dictionaries/значения **общие на весь процесс**, разделяются между shards и handlers.

| Файл | Поле | Тип | Используется |
|---|---|---|---|
| `App/CharacterEngineBot.cs:28` | `_instatnces` | `Dictionary<int, CharacterEngineBot>` | shard registry. **Не concurrent!** Запись только из gateway-thread'а на ShardReady, читается только в init — race маловероятен, но формально не защищён. |
| `App/CharacterEngineBot.cs:34` | `DiscordClient` | `public static DiscordShardedClient` | global handle. Mutable (set один раз в RunAsync), потом read-only. |
| `App/Services/WatchDog.cs:23` | `_watchedUsers` | `ConcurrentDictionary<ulong, CachedWatchedUser>` | rate-limit window per user |
| `App/Services/WatchDog.cs:24` | `_blockedUsers` | `ConcurrentDictionary<ulong, DateTime>` | глобальные баны |
| `App/Services/WatchDog.cs:25` | `_blockedGuildUsers` | `ConcurrentDictionary<(ulong, ulong), object?>` | серверные баны |
| `App/Services/WatchDog.cs:30-31` | `_serviceProvider`, `_running` | refs | инициализированы один раз; **`_running` блокирует повторный запуск**, что плохо при reconnect (см. §10.5) |
| `App/Services/BackgroundWorker.cs:22-23` | `_serviceProvider`, `_running` | refs | то же что у WatchDog |
| `App/Services/MetricsWriter.cs:16` | `_lastAutoMetricReport` | `DateTime` | одно значение, читается/пишется без блокировки. Race возможен (см. §7.6), но критичен только для точности отчёта. |
| `App/Helpers/DatabaseHelper.cs:6` | `DbConnectionString` | `public static string` | **mutable** — кто угодно может его изменить рантайм. Не защищён. Сейчас никто не меняет, но контракт позволяет. |
| `App/Repositories/CacheRepository.cs:18` | `_cachedChannels` | `ConcurrentDictionary<ulong, (bool, DateTime)>` | per-channel метаданные |
| `App/Repositories/CacheRepository.cs:20` | `_cachedGuilds` | `ConcurrentDictionary<ulong, DateTime>` | per-guild last-touch |
| `App/Repositories/CacheRepository.cs:22` | `_cachedUsers` | `ConcurrentDictionary<ulong, DateTime>` | per-user last-touch |
| `App/Repositories/Storages/CachedCharacerInfoStorage.cs:13` | `_cachedCharacters` | `ConcurrentDictionary<Guid, CachedCharacterInfo>` | главный hot-path index |
| `App/Repositories/Storages/CachedWebhookClientsStorage.cs:10` | `_webhookClients` | `ConcurrentDictionary<ulong, CachedWebhookClient>` | reusable Discord webhook clients |
| `App/Repositories/Storages/ActiveSearchQueriesStorage.cs:11` | `_searchQueries` | `ConcurrentDictionary<ulong, SearchQuery>` | active search-paginations |

⚠️ **«Static masquerading as instance»** (см. ARCHITECTURE §5.6). Все `*Storage` классы регистрируются в DI как часть `CacheRepository` (transient), но внутри хранят static fields. То есть `new CachedCharacerInfoStorage()` не создаёт нового хранилища — всё разделяется. Это блокирует unit-тесты с изоляцией состояний.

### 2.2 Per-character mutable state

В `CachedCharacterInfo` (`CachedCharacerInfoStorage.cs:65-120`):

| Поле | Mutability | Защита |
|---|---|---|
| `Id`, `ChannelId`, `WebhookId`, `IntegrationType`, `CachedAt` | `init`-only | — |
| `CallPrefix` | `set` | **нет!** Меняется в `CharacterCommands.Edit` без блокировки |
| `FreewillFactor` | `set` | то же |
| `WideContextLastMessageId` | `set` | то же; читается в hot-path `MessagesHandler.CallCharacterAsync` |
| `HuntedUsers` | `List<ulong>` | публичная мутация через `.Add/Remove`. **Нет блокировки.** Если `/character hunted-users add` в момент, когда `MessagesHandler` итерирует список — `InvalidOperationException` |
| `_queue` (private) | `List<ulong>` | защищён `lock(_queue)` в 4 методах (`QueueIsFullFor/QueueIsTurnOf/QueueAddCaller/QueueRemove`) |

⚠️ **`HuntedUsers` — главный недостаток**: это `List<ulong>`, доступный по `cachedCharacter.HuntedUsers`, мутируется из slash-команды и читается из hot-path без какой-либо синхронизации. В момент гонки можно поймать `InvalidOperationException` ("Collection was modified") в `MessagesHandler.FindHunterCharactersAsync`.

### 2.3 Shared state у одного `MessagesHandler`-инстанса

`MessagesHandler` registered as **transient** — DI создаёт новый инстанс на каждое event. Но handler потом дёргает `CallCharacterAsync` для нескольких персонажей **в параллель** (см. §1.3). То есть **на одном handler-инстансе одновременно крутятся N task'ов**:

| Поле | Тип | Защита |
|---|---|---|
| `_db` | `AppDbContext` | **`SemaphoreSlim _semaphoreSlim`** — все DB-вызовы только под ним |
| `_integrationsDbRepository`, `_charactersDbRepository`, `_cacheRepository` | repos | Они тоже держат `_db` через base. Их методы вызываются под тем же семафором. |
| `_semaphoreSlim` | `SemaphoreSlim(1,1)` | сам себе |

То есть **все N параллельных `CallCharacterAsync` сериализуются через один семафор** при доступе к `_db`. Они работают параллельно только в части upstream-запросов и Discord-API-вызовов.

⚠️ Это — главный bottleneck при freewill-нагрузке: 5 character'ов в одном канале с `freewill=20%` могут одновременно зацепиться за одно сообщение, и все будут ждать доступа к `_db` по очереди.

---

## 3. Примитивы синхронизации — где, как и зачем

### 3.1 `ConcurrentDictionary<TKey, TValue>` — базовый слой

Используется во **всех** static dictionaries (см. §2.1). Гарантии:
- `TryAdd/TryRemove/TryGetValue` — атомарны.
- `GetOrAdd(key, factory)` — атомарен в смысле «либо новый, либо существующий», но `factory` **может вызваться несколько раз** в гонке (CLR-контракт).
- Индексатор `dict[key] = value` — атомарен (overwrite-or-insert).

⚠️ **Что НЕ гарантируется**: транзакция «прочитай + измени + запиши». Например:

```csharp
// Не атомарно:
if (_blockedUsers.ContainsKey(userId)) {
    _blockedUsers[userId] = newDate;
}
```

В коде это место — `WatchDog.BlockUserGloballyAsync` (`WatchDog.cs:84-119`):

```csharp
if (_blockedUsers.TryAdd(userId, blockedUntil) == false) {
    return;   // уже заблокирован, выходим
}
// ↓ далее БД-операции
```

Здесь `TryAdd` работает как «atomic compare-and-set» через возврат `false` — это правильное использование `ConcurrentDictionary`. Большинство мест в коде так и сделано.

### 3.2 `lock(object)` — для мульти-step мутаций

Только два места в коде:

1. **`WatchDog.Validate`** (`WatchDog.cs:201-232`) — `lock (user)`, где `user` это `CachedWatchedUser`. Защищает мульти-step транзакцию: «инкрементировать счётчик, проверить порог, обновить timestamp». `ConcurrentDictionary` здесь не помог бы.

   ⚠️ Подводный камень: **lock на user-объекте**. Если кто-то в другом месте сделает `lock(cachedWatchedUser)` — встретятся. Сейчас только одно место, но без декларативной защиты от случайного второго.

2. **`CachedCharacterInfo._queue`** (`CachedCharacerInfoStorage.cs:67-103`) — `lock (_queue)`. Защищает 4 операции над `List<ulong>`. Список **частный**, lock-объект тоже.

### 3.3 `SemaphoreSlim(1,1)` — async-семафор

Семафоры с лимитом 1 (де-факто async-mutex'ы) в 5 местах:

| Где | Зачем |
|---|---|
| `MessagesHandler._semaphoreSlim` | сериализация EF-вызовов на per-instance `_db` |
| `CacheRepository._dbCallsSemaphore` | аналогично |
| `CharactersDbRepository._deletionLock` | сериализация delete-операций (плюс `try/catch DbUpdateConcurrencyException`) |
| `CachedCharacterInfo`-related в `MessagesHandler` (внутренние queue-locks через lock, не семафоры) | см. выше |

⚠️ **`SemaphoreSlim` — async-aware**, можно `await WaitAsync()`. Но в коде **используется `WaitAsync()` + потом синхронный `.GetAwaiter().GetResult()`** под ним (см. §6 ниже). Это нарушает идею async-семафора и потенциально опасно.

### 3.4 `Task.WaitAll` / `Task.WhenAll` — координация

Бот использует **оба**, и вперемешку:

| Место | Что используется |
|---|---|
| `MessagesHandler.HandleMessageAsync:170` | `Task.WaitAll(callTasks.ToArray())` ← блокирующий |
| `CharacterEngineBot.CacheUsersAndCharacters:239` | `Task.WaitAll(characters, users)` ← блокирующий |
| `CharacterEngineBot.RegisterCommandsToAllGuildsAsync:261-272` | `Parallel.ForEachAsync` ← async |
| Везде остальное | `await taskA; await taskB;` или `await Task.WhenAll(...)` |

⚠️ `Task.WaitAll` в async-контексте — анти-паттерн. Кандидат на замену.

### 3.5 Implicit thread-safety через CLR

- **Static field initializers**: `static readonly Foo X = new Foo();` — CLR гарантирует ровно один вызов конструктора, thread-safe. Используется в `IntegrationsHub`, `CommonHelper.CommonHttpClient`, и т.д.
- **`HttpClient`** — единственный экземпляр в `CommonHelper.CommonHttpClient`, переиспользуется. Это правильный паттерн (избегает port-exhaustion из-за `new HttpClient()` в loop).

---

## 4. Шесть слоёв синхронизации одного hot-path вызова

При получении одного сообщения в канале мы проходим через шесть concurrency-точек:

```
[Layer 1] gateway-thread (Discord.Net) → handler возвращает Task.CompletedTask
            ↓
[Layer 2] thread-pool thread (Task.Run) → начало HandleMessageAsync
            ↓
[Layer 3] WatchDog.ValidateUser:
            • ConcurrentDictionary.TryGet (blocked?)
            • lock(CachedWatchedUser) для rate-limit increment
            ↓
[Layer 4] cachedCharacters.GetAll(channelId) → snapshot из ConcurrentDictionary
            ↓
[Layer 5] для каждого триггера:
            CallCharacterAsync(spawnedChar):
            ├─ semaphore.WaitAsync (per-handler)
            │     • _integrationsDbRepository.GetGuildIntegrationAsync (sync GetResult!)
            │     • semaphore.Release
            ├─ cachedCharacter.QueueAddCaller (lock(_queue))
            ├─ poll loop (Task.Delay 500ms × до 2 минут):
            │     • cachedCharacter.QueueIsTurnOf (lock(_queue))
            ├─ semaphore.WaitAsync
            │     • _charactersDbRepository.GetSpawnedCharacterByIdAsync (sync GetResult!)
            │     • semaphore.Release
            ├─ semaphore.WaitAsync (если messageFormat null)
            │     • db.DiscordChannels.Include(...).FirstAsync (await ✓)
            │     • semaphore.Release
            ├─ ...build context window (если freewill)... → Discord-API: channel.GetMessagesAsync
            ├─ IntegrationsHub.GetChatModule(type).CallCharacterAsync:
            │     • upstream HTTP (Sakura/CAI/OR) — 5..15 sec
            │     • для OR: открывает свой AppDbContext параллельно
            │     • для Sakura/CAI: мутирует character.*ChatId напрямую
            ├─ webhook send (Discord-API через cached client)
            ├─ semaphore.WaitAsync
            │     • _charactersDbRepository.UpdateSpawnedCharacterAsync (sync GetResult!)
            │     • semaphore.Release
            └─ cachedCharacter.QueueRemove (lock(_queue))
            ↓
[Layer 6] Task.WaitAll(callTasks) ← блокирующий wait в async-контексте
```

Между Layer 5 и Layer 6 — параллелизм по триггерам. Внутри Layer 5 — **строго последовательная цепочка**, но с тремя точками блокировки и одной долгой работой (upstream HTTP), за время которой thread-pool thread ничего полезного не делает (просто ждёт response).

---

## 5. Цепочка семафоров `MessagesHandler.CallCharacterAsync` — детальная

Этот метод — самое сложное concurrency-устройство в коде. Разберу его от начала до конца с указанием состояний.

### 5.1 Pseudo-code с разметкой блокировок

```csharp
async Task CallCharacterAsync(ISpawnedCharacter sc, SocketUserMessage msg, bool isIndirect) {
    // [A] NSFW pre-check (no lock)
    if (sc.IsNfsw && !channel.IsNsfw) { reply; return; }

    // [B] DB read под семафором — но GetResult синхронный!
    IGuildIntegration? integration;
    await _semaphoreSlim.WaitAsync();
    try {
        integration = _integrationsDbRepository
            .GetGuildIntegrationAsync(sc)
            .GetAwaiter().GetResult();        // ⚠ sync await под async lock
    } finally { _semaphoreSlim.Release(); }
    if (integration is null) return;

    // [C] Cached read (lock-free)
    var cached = _cacheRepository.CachedCharacters.Find(sc.Id)!;

    // [D] FIFO-очередь на per-character уровне
    if (cached.QueueIsFullFor(authorId)) return;          // lock(_queue)
    cached.QueueAddCaller(authorId);                      // lock(_queue)

    try {
        // [E] Busy-wait через Task.Delay — 500мс × до 2 минут
        var sw = Stopwatch.StartNew();
        while (!cached.QueueIsTurnOf(authorId)) {         // lock(_queue) на каждой итерации
            if (sw.Elapsed.TotalMinutes >= 2) return;
            await Task.Delay(500);
        }

        // [F] Force-reload из БД — снова семафор + GetResult
        await _semaphoreSlim.WaitAsync();
        try {
            sc = _charactersDbRepository
                .GetSpawnedCharacterByIdAsync(sc.Id)
                .GetAwaiter().GetResult()!;
        } finally { _semaphoreSlim.Release(); }
        if (sc is null) return;

        // [G] Optional delay для bot/webhook авторов (anti-loop)
        var delay = author.IsBot || author.IsWebhook
                  ? Math.Max(5, sc.ResponseDelay)
                  : sc.ResponseDelay;
        if (delay > 0) await Task.Delay(delay * 1000);

        // [H] messagesFormat lookup — снова семафор, но здесь await правильный
        var messageFormat = sc.MessagesFormat;
        if (messageFormat is null) {
            await _semaphoreSlim.WaitAsync();
            try {
                var formats = await _db.DiscordChannels.Include(...).Where(...).Select(...)
                    .FirstAsync();                        // ✓ async await
                messageFormat = formats.ChannelMessagesFormat
                             ?? formats.GuildMessagesFormat
                             ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
            } finally { _semaphoreSlim.Release(); }
        }

        // [I] Wide-context (для indirect только) — Discord-API за пределами семафора
        string userMessage;
        if (isIndirect && sc.FreewillContextSize != 0) {
            var msgs = await channel.GetMessagesAsync(20).FlattenAsync();   // Discord-API call
            userMessage = ... build ...;
        } else {
            userMessage = ReformatUserMessage(msg, sc.CallPrefix, messageFormat);
        }

        // [J] Главный upstream-вызов (БЕЗ семафора!)
        var response = await IntegrationsHub.GetChatModule(type)
                                            .CallCharacterAsync(sc, integration, userMessage);
        // ↑ Это 5-15 секунд upstream HTTP. Семафор не держится.
        // ↑ Для Sakura/CAI: МУТИРУЕТ sc.SakuraChatId/CaiChatId
        // ↑ Для OpenRouter: открывает СВОЙ DbContext параллельно к нашему _db

        // [K] Webhook-send через cached client (без семафора)
        try {
            var webhook = _cacheRepository.CachedWebhookClients.FindOrCreate(...);
            var active = new ActiveCharacterDecorator(sc, webhook);
            messageId = await active.SendMessageAsync(...);   // Discord-API
        } catch (...) {
            // если webhook исчез — удаляем из кэша + удаляем character
            _cacheRepository.CachedWebhookClients.Remove(...);
            _cacheRepository.CachedCharacters.Remove(...);
            await _semaphoreSlim.WaitAsync();
            try {
                await _charactersDbRepository.DeleteSpawnedCharacterAsync(sc.Id);
            } finally { _semaphoreSlim.Release(); }
            throw;
        }

        // [L] Mutate-and-save — снова семафор + GetResult
        sc.LastCallerDiscordUserId = authorId;
        sc.LastDiscordMessageId    = messageId;
        sc.LastCallTime            = DateTime.Now;
        sc.MessagesSent++;
        await _semaphoreSlim.WaitAsync();
        try {
            await _charactersDbRepository.UpdateSpawnedCharacterAsync(sc);   // ✓ await
        } finally { _semaphoreSlim.Release(); }

        // [M] Метрика fire-and-forget
        MetricsWriter.Write(MetricType.CharacterCalled, ...);

    } finally {
        cached.QueueRemove(authorId);                     // lock(_queue)
    }
}
```

### 5.2 Полный список захватов и опасностей

| Step | Lock | Async/Sync | Опасность |
|---|---|---|---|
| B | `_semaphoreSlim` | sync `GetResult()` | deadlock-prone в SyncContext-окружении |
| D | `lock(_queue)` × 2 | sync | OK (короткий) |
| E | `lock(_queue)` × N | sync | busy-poll — нагрузка thread-pool пока ждёт |
| F | `_semaphoreSlim` | sync `GetResult()` | то же что B |
| H | `_semaphoreSlim` | **async await** ✓ | OK |
| I | (нет) | async | OK |
| J | (нет) | async | долгий upstream — 5-15 сек |
| K | (нет) | async | возможна гонка cache-eviction (см. §7) |
| L | `_semaphoreSlim` | **async await** ✓ | OK |
| M | (нет) | fire-and-forget | OK для метрик |

⚠️ **Несимметрия B/F vs H/L** — в одних и тех же местах используется и `GetResult()`, и `await`. Видимо, остатки от рефакторинга. Все B/F **обязаны** быть переписаны на `await` для безопасности.

### 5.3 Multi-trigger одновременная обработка

При freewill+reply на одно сообщение `MessagesHandler.HandleMessageAsync` создаёт N задач, каждая из которых выполняет полный цикл выше. Семафор `_semaphoreSlim` — **один на handler-инстанс**, поэтому N задач будут стоять в очереди на B, F, H, L. Это де-факто **сериализация** всех DB-операций в рамках одного сообщения.

Параллельно — только J (upstream calls — действительно идут в N HTTP-параллельно), K (Discord-API send) и upstream-side в OpenRouterModule (отдельный DbContext).

⚠️ **Это значит**: главная польза от множественности триггеров — **параллельные upstream-вызовы**. БД-нагрузка остаётся последовательной по-канальная, но Discord-сторона видит N webhook-sends за ~то же время, что один.

### 5.4 Поведение под нагрузкой

**Нагрузка 1 сообщения/сек на канал**: всё работает гладко.

**Нагрузка 10 сообщений/сек на канал** (горячий канал с активным freewill):
- Семафор `_semaphoreSlim` в hot lock — 5 захватов на сообщение × 10 сообщений = 50 lock-attempts/сек.
- Очередь `_queue` на каждый персонаж — 5 user'ов в очереди = drop-rate начинается.
- thread-pool занят: 10 × {wait + upstream 5-15 sec} = 50-150 потоков одновременно. На 64-core CPU это норма; на 4-core может выгребать.

**Нагрузка >100 сообщений/сек** (1000-гильдийный шард, активные часы):
- N`_semaphoreSlim` (по одному на handler-инстанс) — каждое сообщение получает свой handler, поэтому семафоры не конкурируют между сообщениями.
- Но `_blockedUsers`/`_watchedUsers` (`WatchDog`) и `_cachedCharacters` (storage) — **общие на процесс**. ConcurrentDictionary справляется до ~миллиона ops/sec.
- thread-pool: высокий риск exhaustion при `Task.WaitAll` (см. §1.3).

---

## 6. Шесть способов нарушить async-инварианты

В `MessagesHandler.CallCharacterAsync` есть три проблемных вызова в виде `_repo.Method().GetAwaiter().GetResult()` под `await _semaphoreSlim.WaitAsync()`. Что **именно** там неправильно:

### 6.1 Контракт `SemaphoreSlim`

`SemaphoreSlim.WaitAsync()` возвращает `Task` — поток освобождается, ждёт «асинхронно». При выходе из блока — `Release()` на любом потоке. Контракт:

```csharp
await sem.WaitAsync();        // освобождает поток
try {
    await someAsyncOp();      // ✓ async внутри
} finally {
    sem.Release();
}
```

### 6.2 Что делает `GetResult()` в этом контексте

```csharp
await sem.WaitAsync();
try {
    var x = SomeAsyncOp().GetAwaiter().GetResult();   // ⚠ sync wait на async task
} finally {
    sem.Release();
}
```

`GetResult()` — синхронный wait. На текущем thread-pool thread'е. Что значит:

1. **Ничего не освобождается**. Поток занят всё время `SomeAsyncOp`.
2. **Если внутри `SomeAsyncOp` где-то стоит `await ConfigureAwait(true)`** — продолжение хочет вернуться на оригинальный SynchronizationContext. Если такого контекста нет (как в bot'е) — продолжение бежит на capturer thread (тот же thread-pool thread, откуда был старт). **Если этот thread сейчас в `GetResult`** — deadlock.

### 6.3 Почему сейчас работает

В нашем боте:
- Discord.Net (3.17) — везде `ConfigureAwait(false)` в библиотеке. Continuations не возвращаются на конкретный thread.
- Npgsql — везде `ConfigureAwait(false)`.
- EF Core — то же.
- Нет UI thread'а или ASP.NET-context'а, который захватил бы SyncContext.

То есть поток, на котором стоит `GetResult`, ничего конкретного не ждёт — continuation просто берёт любой свободный thread из пула.

### 6.4 Что может сломать

- **Любая зависимость, которая не использует `ConfigureAwait(false)`** в своих `await`-ах. Например, если кто-то решит добавить `Microsoft.Extensions.Hosting` (ASP.NET-style SyncContext) или `Avalonia`/`WPF` (UI SyncContext) для admin-панели — сразу deadlock.
- **`Microsoft.AspNetCore` middleware** в боте — никогда сейчас не используется, но при переходе на `IHost` появится.
- Любой код, который явно делает `Task.Run(() => ... .Wait())` или подобное наследие — может встретиться в submodule-клиентах, не контроллируется отсюда.

### 6.5 Третья причина не делать так — perf

`GetAwaiter().GetResult()` пожирает thread-pool thread на всё время операции. На N concurrent сообщений с 5 GetResult'ами → 5N занятых потоков. При нагрузке это упирается в ThreadPool.SetMinThreads.

**Кандидат на правку**: B, F → переписать на `await`. Один-два часа работы, тестирование на нагрузке.

---

## 7. Каталог гонок

Ниже — **известные** и **потенциальные** race conditions. Помечены приоритеты:
- 🔴 актуальная (легко воспроизводится)
- 🟡 потенциальная (требует определённых условий)
- 🟢 теоретическая (маловероятна, но не невозможна)

### 7.1 🔴 `HuntedUsers` — concurrent enumerate vs mutate

**Где**: `MessagesHandler.FindHunterCharactersAsync` (строки 508-517) итерирует `cachedCharacters.Where(cc => cc.HuntedUsers.Contains(authorId))`. Параллельно `CharacterCommands.HuntedUsers` мутирует `cachedCharacter.HuntedUsers.Add(huntedUserId)` или `Remove`.

**Симптом**: `InvalidOperationException("Collection was modified")` в `MessagesHandler`.

**Триггер**: модератор пишет `/character hunted-users add`, а в это же время в канале активный чат.

**Защита**: нет.

**Исправление**: заменить `List<ulong>` на `ConcurrentBag<ulong>` или `ImmutableHashSet<ulong>` с copy-on-write.

### 7.2 🔴 Cached vs DB рассинхрон при `/character edit`

**Где**: `CharacterCommands.Edit` обновляет одновременно `spawnedCharacter.X` (EF entity) и `cachedCharacter.X` (in-memory record). Не транзакционно.

**Симптом**: если два модератора одновременно делают `/character edit call-prefix`, последний победит в кэше, а в БД может оказаться значение от первого (или оба, в зависимости от ordering).

**Триггер**: два concurrent edit на одного персонажа.

**Защита**: нет — даже EF concurrency token не настроен.

**Исправление**: либо optimistic concurrency через `[Timestamp]` колонку, либо queue-based serialization для edit'ов на персонажа.

### 7.3 🟡 Webhook-cache eviction в момент использования

**Где**: `BackgroundWorker.ClearCache` каждые 5 минут эвиктит `_webhookClients`, у которых `LastHitAt > 10мин`. Параллельно `MessagesHandler.CallCharacterAsync` уже вытащил клиент через `FindOrCreate` и держит в локальной переменной.

**Симптом**: после `Remove` из cache локальная ссылка всё ещё работает (объект жив, GC не удалит). НО если в этот момент кто-то ещё захочет тот же webhook — получит **новый** `DiscordWebhookClient` (на тот же id+token). Два клиента на один webhook — корректно работает, но впустую.

**Защита**: нет, и не нужна — это бенigна гонка. Зафиксирована для понимания.

### 7.4 🟡 Двойной insert одного channel/guild

**Где**: `CacheRepository.EnsureChannelCached` и `EnsureGuildCached` используют `_cachedChannels.TryAdd(channel.Id, ...)` для exclusivity. Если возвращается `false` — выходит. Если `true` — лезет в БД через `_dbCallsSemaphore` и проверяет `await DB.DiscordChannels.FindAsync(channel.Id)`.

**Симптом**: возможна гонка между двумя shards (или просто двумя concurrent событиями), которые оба прошли `TryAdd` (не одновременно — у одного будет `false`). После `TryAdd=true` идёт `FindAsync` под семафором, и если row отсутствует — INSERT. Защита от дубля INSERT'а — Postgres PK constraint, **который кидает `DbUpdateException` и handler упадёт** (а в логе появится stack trace).

**Защита**: на уровне dictionary — есть. На уровне БД — Primary Key constraint поймает. Но красиво это не обработано.

**Исправление**: try/catch на `DbUpdateException` (unique violation) → swallow + reload row из БД.

### 7.5 🟡 Concurrent insert в `Metric` через `MetricsWriter.Write`

**Где**: `MetricsWriter.WriteAsync` создаёт **новый AppDbContext** на каждую запись. Из 10 параллельных событий — 10 параллельных connections.

**Симптом**: при пиковой нагрузке connection pool exhaustion. Postgres `max_connections` по умолчанию 100; при 100+ параллельных метриках — ошибки coнnect.

**Защита**: нет.

**Исправление**: батчинг через `Channel<Metric>` + единственный writer-task.

### 7.6 🟡 `MetricsWriter._lastAutoMetricReport` — non-atomic update

**Где**: `BackgroundWorker.MetricsReport`:
```csharp
var sinceDt = MetricsWriter.GetLastAutoMetricReport();
// ... query metrics since sinceDt ...
MetricsWriter.SetLastAutoMetricReport(DateTime.Now);
```

Между Get и Set — другой thread мог бы обновить `_lastAutoMetricReport`. Но **нет других writer'ов** — это поле обновляется только в `MetricsReport`-loop'е (1 раз в час). То есть гонка возможна только если admin вызвал `/reportmetrics` параллельно с auto-report. Но `BotAdminCommandsHandler.ReportMetricsAsync` **не обновляет** `_lastAutoMetricReport`. Так что race нет.

**Статус**: не race по факту. Но архитектурно поле — public mutable без блокировки.

### 7.7 🟢 `_instatnces` dictionary — non-concurrent

**Где**: `CharacterEngineBot._instatnces` — `Dictionary<int, CharacterEngineBot>`, не `ConcurrentDictionary`.

**Симптом**: если бы Discord.Net выдал `ShardReady` **параллельно** для двух shards — гонка на `_instatnces.Add`.

**Защита**: нет, **но Discord.Net это не делает**. Согласно его внутренней реализации, `ShardReady` events сериализуются.

**Исправление**: всё равно заменить на `ConcurrentDictionary` для безопасности контракта.

### 7.8 🟡 Pre-load + первое сообщение

**Где**: `Program.Main` → `Migrator.Run` → `CacheUsersAndCharacters` → `CharacterEngineBot.RunAsync` → `LoginAsync`. Сообщения могут начать приходить **до** завершения `CacheUsersAndCharacters`.

Точнее: `CacheUsersAndCharacters` вызывается **до** `LoginAsync`, и блокирует через `Task.WaitAll`. Но `Task.WaitAll(characters, users)` — **синхронный**. То есть Login не начнётся до завершения. Гонки нет.

**Статус**: безопасно из-за блокирующего wait. Но риск появляется, если кто-то заменит `Task.WaitAll` на `await Task.WhenAll`.

### 7.9 🟡 Spawn-and-orphan webhook

**Где**: `IntegrationsMaster.SpawnCharacterAsync`:
```csharp
var webhook = await InteractionsHelper.CreateDiscordWebhookAsync(...);   // Discord-API: создан
var webhookClient = new DiscordWebhookClient(...);
_cacheRepository.CachedWebhookClients.Add(webhook.Id, webhookClient);
try {
    newSpawnedCharacter = await _charactersDbRepository.CreateSpawnedCharacterAsync(...);
} catch {
    _cacheRepository.CachedWebhookClients.Remove(webhook.Id);
    try { await webhookClient.DeleteWebhookAsync(); } catch { /* care not */ }
    throw;
}
```

Здесь **есть** rollback, но если процесс упадёт **между** `CreateWebhookAsync` и `_cachedWebhookClients.Add` — webhook создан в Discord, но не зарегистрирован у нас. Orphan.

**Симптом**: в канале появляется webhook без привязки к боту.

**Триггер**: kill -9 / OutOfMemoryException в неподходящий момент.

**Защита**: нет (нет персистентного staging-state).

**Исправление**: либо двух-фазная транзакция через `StoredAction`, либо периодический cleanup orphan webhooks (`channel.GetWebhooksAsync` ↔ `db.SpawnedCharacters`).

### 7.10 🟡 Remove-and-orphan webhook

**Где**: `CharacterCommands.RemoveCharacter`:
```csharp
var deleteSpawnedCharacterAsync = _charactersDbRepository.DeleteSpawnedCharacterAsync(sc.Id);
// → fire and forget!
var webhookClient = CachedWebhookClientsStorage.Find(sc.WebhookId);
if (webhookClient is not null) {
    try { await webhookClient.DeleteWebhookAsync(); } catch { /* care not */ }
}
await deleteSpawnedCharacterAsync;
```

Webhook удалён из Discord, но `DeleteSpawnedCharacterAsync` ещё не завершилась. Если процесс упадёт между — БД-запись остаётся, webhook'а нет. **Bot будет помнить о персонаже, но не сможет ему писать**.

**Симптом**: попытка вызова такого персонажа упадёт на `webhookClient.SendMessageAsync` → `Unknown Webhook` → `MessagesHandler` распознаёт через `ValidateWebhookException` и удалит row. То есть само-исцеляется на следующем сообщении.

**Защита**: само-исцеление в hot-path.

**Состояние**: приемлемо.

### 7.11 🔴 OpenRouter `attempt` не инкрементируется

См. CONNECTORS.md §10.3. Не concurrency-bug, но в hot-path может приводить к hang (что эквивалентно). Не исправляется через locks — нужен фикс в коде.

### 7.12 🟡 `WatchDog._serviceProvider` static — не пересоздаётся при reconnect

**Где**: `WatchDog.RunAsync` — выставляет `_serviceProvider = serviceProvider` один раз и `_running = true`. Если admin-shard reconnect'ится и создаст новый `CharacterEngineBot` (новый ServiceProvider) — **второй вызов `WatchDog.RunAsync` ничего не сделает** (ранний return по `_running`).

**Симптом**: после reconnect WatchDog всё ещё держит **старый** ServiceProvider. При попытке его использования (для нового `AppDbContext`) старый scope даст disposed exception. БД-операции в WatchDog (`BlockUserGloballyAsync`, `UnblockUserGloballyAsync`) перестают работать.

**Триггер**: Discord ratelimit / network issue → shard reconnect.

**Защита**: нет.

**Исправление**: `WatchDog.RunAsync` должен **перезаписывать** `_serviceProvider` без ранний return по `_running`.

### 7.13 🟡 `BackgroundWorker._running` — то же

Та же проблема, что в §7.12.

### 7.14 🟢 `DbConnectionString` — public mutable

`DatabaseHelper.DbConnectionString` — `public static string` field, не readonly. Любой код может его перезаписать. Сейчас никто не делает, но контракт это позволяет.

**Исправление**: сделать `init`-only через property или конкретно readonly после перехода на `IOptions<DatabaseOptions>`.

---

## 8. Каталог deadlock-рисков

### 8.1 🟡 SemaphoreSlim + GetResult ⊕ SyncContext

См. §6. Сейчас не воспроизводится, но появится при добавлении любой зависимости с SyncContext.

### 8.2 🟢 Per-character queue: каскадные wait-цепочки

`cachedCharacter.QueueIsTurnOf(authorId)` ждёт пока текущий user не выйдет. Если **тот** user в свою очередь ждёт **другую** очередь (другой персонаж в том же канале с очередью на того же user'а) — теоретически круговая зависимость.

На практике: один user не может стоять одновременно в двух очередях, потому что `QueueIsFullFor(userId)` проверяет «уже стоит» и `return false` (отдрасывает).

**Состояние**: безопасно за счёт early-return.

### 8.3 🟡 EF Core: один DbContext, разные операции

`AppDbContext` НЕ thread-safe. Если две task'и параллельно вызывают `_db.SaveChangesAsync()` — `InvalidOperationException("A second operation was started on this context...")`.

В `MessagesHandler` это защищается семафором. Но если кто-то добавит новый код без семафора — мгновенный bug.

**Исправление**: либо контекст per task, либо документированная политика «всё через семафор» + анализатор.

### 8.4 🟢 Discord.Net rate-limit + SemaphoreSlim

Discord.Net `RetryRatelimit` — внутри библиотеки делает retry с exponential backoff. Если webhook-send упёрся в rate-limit, он подождёт 1-30 сек. В этот момент `MessagesHandler.CallCharacterAsync` уже **за пределами** семафора (этап K, см. §5), поэтому БД-операции не блокируются. Но сам character-call держит per-character queue (через `cached.QueueRemove(authorId)` в finally) — то есть остальные user'ы в очереди ждут.

**Состояние**: приемлемо, но при массовых rate-limit'ах нагрузка на queue растёт.

### 8.5 🟢 `Migrator.Run` + Postgres lock

`Migrator.Run` делает синхронный `db.Database.MigrateAsync().GetAwaiter().GetResult()`. Если БД уже под миграцией от другого инстанса (например, blue-green deploy) — повисание.

**Состояние**: для одной replica — не происходит. При HA — нужен advisory lock (стандартная практика).

---

## 9. EF Core thread-safety

### 9.1 Контракт

`DbContext` — **не** thread-safe. Документация EF Core явно говорит: «no two threads can use the same context concurrently».

### 9.2 Где это нарушается / соблюдается

| Место | DbContext lifetime | Защита |
|---|---|---|
| `MessagesHandler._db` | Per handler-instance (DI transient, новый на каждое event) | `SemaphoreSlim` сериализует |
| `CharactersDbRepository.DB`, `IntegrationsDbRepository.DB`, `CacheRepository.DB` | Тот же `_db`, передан через ctor | Используются только под `_semaphoreSlim` (от MessagesHandler) или под `_dbCallsSemaphore` (свой в CacheRepository) |
| `OpenRouterModule.CallCharacterAsync` | Свой `new AppDbContext(_connectionString)` | OK — изолирован per-call |
| `MetricsWriter.WriteAsync` | Свой `new AppDbContext(_connectionString)` | OK |
| `Migrator.Run` | Свой `using var db = new AppDbContext(...)` | OK |
| `WatchDog.RunAsync`/`BlockUserGloballyAsync`/`UnblockUserGloballyAsync` | Через `_serviceProvider.GetRequiredService<AppDbContext>()` | **NO LOCK** ⚠ |
| `BackgroundWorker.MetricsReport`/`RunStoredActions`/`RevalidateBlockedUsers` | Через `_serviceProvider.GetRequiredService<AppDbContext>()` | **NO LOCK** ⚠ |

### 9.3 ⚠ WatchDog/BackgroundWorker — DbContext без блокировки

В `WatchDog.BlockUserGloballyAsync`:
```csharp
await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
db.BlockedUsers.Add(...);
await db.SaveChangesAsync();
```

`GetRequiredService<AppDbContext>()` для transient — создаёт **новый** инстанс. То есть гонка-safe, контекст per-call. **Если** регистрация в DI остаётся transient. Если кто-то в будущем переведёт `AppDbContext` в singleton/scoped — мгновенный bug.

**Состояние**: безопасно через DI-конфигурацию transient, но не явно защищено архитектурно.

### 9.4 Optimistic concurrency не настроен

Ни в одной EF-сущности нет `[Timestamp]` колонки или `IsConcurrencyToken=true`. Все обновления — **last-writer-wins** (LWW). Это значит:

- Два concurrent `/character edit` — потеряют один из апдейтов.
- Manual update в БД → in-memory tracker не знает → следующий `SaveChanges` затрёт.

Только в `CharactersDbRepository.DeleteSpawnedCharacterAsync` есть catch для `DbUpdateConcurrencyException`. Это намёк, что когда-то проблема была видна, но решения «по полной» не сделано.

**Исправление**: добавить `[Timestamp] public byte[] RowVersion { get; set; }` в `*SpawnedCharacters` (минимум) и обработку `DbUpdateConcurrencyException` через retry-loop или явный refresh.

---

## 10. Sharding и его гонки

### 10.1 Что такое shard в нашем коде

`DiscordShardedClient` создаёт N `DiscordSocketClient`-ов (один per shard). Discord-сторона делит гильдии между ними по shard-id (computed from guild_id). На каждое `ShardReady` бот **создаёт** новый `CharacterEngineBot` со своим `ServiceProvider`.

### 10.2 Что общее, что разделено

| Что | Per-shard | Global |
|---|---|---|
| Gateway socket | ✅ | — |
| `CharacterEngineBot` instance | ✅ | — |
| `ServiceProvider` | ✅ | — |
| Handlers (`MessagesHandler`, `ButtonsHandler`, etc.) | ✅ (через DI) | — |
| `_db` (AppDbContext) | ✅ (transient через DI) | — |
| Static `_cachedCharacters`, `_webhookClients`, etc. | — | ✅ |
| `WatchDog._blockedUsers/_watchedUsers` | — | ✅ |
| `IntegrationsHub.SakuraAiModule`, etc. | — | ✅ |
| `BackgroundWorker._running`/`WatchDog._running` | — | ✅ (admin-shard only) |

То есть **обработка events — per-shard**, **state — global**. Это даёт N gateway-thread'ов, дёргающих общий state.

### 10.3 Race scenarios между shards

**Один user в двух гильдиях на разных shards** одновременно пишет в обоих чатах:

- `WatchDog.ValidateUser` для shard A: `_watchedUsers.GetOrAdd(userId, ...)` — может одновременно с shard B.
- `_watchedUsers` — `ConcurrentDictionary`, `GetOrAdd` атомарен, но `lock(user)` для increment — short critical section, очередь minimal.

**Состояние**: ОК. Дизайн правильный.

**Один character в одной гильдии** — нет, character привязан к channel'у, channel — к одной guild, guild — к одному shard. Нет cross-shard конфликта.

### 10.4 Gateway-thread vs background-loop конфликт

- `MetricsReport` (background) делает `db.Metrics.Where(...).ToArrayAsync()`.
- `MessagesHandler` (gateway-driven) делает `MetricsWriter.Write(...)` → INSERT в `db.Metrics`.

Они на разных DbContext'ах (см. §9.2), читают/пишут одну таблицу. Postgres-сторона разрулит через MVCC. На уровне C# — нет конфликта.

### 10.5 ⚠ Reconnect и stuck WatchDog/BackgroundWorker

См. §7.12 / §7.13. Главная архитектурная проблема шардинга: если admin-shard reconnect'ится, новый `CharacterEngineBot` создан, но static `WatchDog._running == true` и `BackgroundWorker._running == true` блокируют повторный запуск. Внутри они держат **старый** `_serviceProvider`, который привязан к умершему shard-instance.

**Симптом**: после долгого reconnect bot выглядит живым (отвечает на сообщения), но:
- background-loops продолжают работать со старым ServiceProvider — DB-вызовы могут падать на disposed objects.
- `WatchDog.BlockUserGloballyAsync` не сможет писать в БД.
- `MetricsReport` не сможет постить в logs-канал.

**Степень опасности**: высокая. Это **product-killer** при долгой работе.

**Исправление**: `_running` должно либо сбрасываться при `ShardDisconnected`, либо вообще не использоваться (просто перезаписывать `_serviceProvider`).

---

## 11. Orphan-state каталог

«Orphan» = состояние, где части системы рассинхронизированы (БД vs Discord vs cache).

| Сценарий | Что остаётся orphan'ом | Само-исцеляется? |
|---|---|---|
| Spawn падает между `CreateWebhookAsync` и `db.SaveChanges` | webhook в Discord без БД-row | **нет** (см. §7.9) |
| Spawn падает после `db.SaveChanges`, до `_cachedCharacters.Add` | row в БД без записи в кэше | да, через рестарт (CacheUsersAndCharacters) |
| Process kill во время Remove (DELETE row, потом DELETE webhook) | webhook в Discord без БД-row | **нет** |
| Process kill во время Remove (DELETE webhook, потом DELETE row) | БД-row без webhook'а | да, через next CallCharacter (см. §7.10) |
| Edit-name создал новый webhook, но не обновил `WebhookId` | старый WebhookId в БД, новый в Discord | **нет** (см. CONNECTORS.md §10) |
| `/integration remove removeAssociatedCharacters:false` обнуляет FreewillFactor, но не удаляет character | character в кэше с правильным prefix, но без integration | **симулирует** молчание; explicit reply работать не будет |
| Cache eviction для `_cachedCharacters` | character в БД, но не в кэше | автоматически восстанавливается на startup, но НЕ при runtime — `MessagesHandler` его не найдёт до рестарта |
| OpenRouter — character spawned, история не записана | greeting не в `CharacterChatHistory` | **накопляется** при первом сообщении (см. §8.2 в CONNECTORS) |
| Sakura/CAI — `*ChatId` обновлён в module, но `UpdateSpawnedCharacterAsync` упала | upstream-сторона помнит chat, мы — нет | да: следующий вызов создаст новый chat upstream-side, контекст потерян |

**Главные orphan-риски** (помечены как «нет»): три сценария вокруг webhook lifecycle. Все они требуют либо двух-фазной транзакции (`StoredAction`-based), либо периодического orphan-cleanup'а.

---

## 12. Атак-векторы через гонки

Если admin-абьюзер захочет специально сломать бот:

1. **`/character edit` flood** — два concurrent edit на один character. LWW-bug превратит race в потерю данных.
2. **`/character hunted-users add` + флуд сообщений** — race на `HuntedUsers` `List<ulong>` (см. §7.1) → exception в hot-path.
3. **`/character remove` + сразу `/character spawn` тот же character** — orphan webhook возможен.
4. **Rapid clicks `Spawn → Cancel`** — потенциально несколько webhook'ов в Discord без привязки.
5. **Spam в канале** (>15/30s) — попадание под WatchDog бан (легитимная защита).
6. **Очень длинный prompt в `OpenRouterSettings` модалке** — но модалка-input ограничена 4000 символов (Discord limit).

**Защита от 1-3 — это unit-тесты + рефакторинг**, не runtime-фильтр.

---

## 13. План рефакторинга concurrency-модели

В порядке цены/выгоды.

### Этап A — точечные фиксы (1-2 дня, низкий риск)

A1. **Заменить `Task.WaitAll` на `Task.WhenAll`** в `MessagesHandler.HandleMessageAsync:170` и `CharacterEngineBot.CacheUsersAndCharacters:239`. Один `await` каждый.

A2. **Заменить `_repo.X().GetAwaiter().GetResult()` на `await _repo.X()`** в `MessagesHandler.CallCharacterAsync` (3 места). Снимает риск sync-context deadlock.

A3. **Заменить `Dictionary` на `ConcurrentDictionary`** в `CharacterEngineBot._instatnces`. Одна строка.

A4. **Защитить `HuntedUsers`** — заменить `List<ulong>` на `ConcurrentBag<ulong>` или `ImmutableHashSet<ulong>` с copy-on-write. Изменение ~10 callsite'ов.

A5. **`WatchDog._running`/`BackgroundWorker._running` — убрать early return** или сбрасывать на reconnect. Нужно проследить, что не вызовет double-startup background-loops.

A6. **`OpenRouter.attempt` инкрементировать в catch-блоке**.

### Этап B — стандартизация EF-доступа (3-5 дней, средний риск)

B1. **Добавить `[Timestamp]` колонку в `*SpawnedCharacters`** + retry-loop на `DbUpdateConcurrencyException`. Защита от LWW-rasing для edit-команд.

B2. **DbContext per request** — explicit лог при использовании одного контекста несколько раз.

B3. **Удалить `_semaphoreSlim` из `MessagesHandler`** — заменить на per-call DbContext'ы (один per CallCharacterAsync). Снимает 5 захватов семафора, упрощает код.

### Этап C — переход на `Microsoft.Extensions.Hosting` (1-2 недели, высокий риск)

C1. **`IHost` + `IHostedService`** для:
   - `BotHost` (вместо CharacterEngineBot.RunAsync)
   - `BackgroundWorkerService` (с `CancellationToken`)
   - `WatchDogService`

C2. **`IServiceProvider` per shard** убирается, остаётся один на процесс. Shards становятся scope'ами.

C3. **Static `*Storage` → DI-singletons**. Снимает блокер на тестируемость.

C4. **`graceful shutdown`** через `IHostApplicationLifetime.ApplicationStopping` — даёт текущим CallCharacter'ам завершиться, потом закрывает gateway, потом останавливает background-loops.

### Этап D — concurrency-aware webhook lifecycle (1-2 недели, средний риск)

D1. **Spawn — двухфазная транзакция**:
   - PHASE 1: `StoredAction(SpawnCharacter, payload)` записывается в БД.
   - PHASE 2: `CreateWebhookAsync` → если успех, обновить `StoredAction.Status=Finished` + создать `*SpawnedCharacter` row.
   - Background-cleanup: ищет `Pending`-строки старше 5 минут → удаляет webhook + cancel.

D2. **Remove — обратный порядок**:
   - PHASE 1: пометить `*SpawnedCharacter.Deleting=true`.
   - PHASE 2: `DeleteWebhookAsync`.
   - PHASE 3: `DELETE FROM *SpawnedCharacters`.
   - Background-cleanup: ищет `Deleting=true` старше 5 минут → завершает.

D3. **Edit-name** — обновлять `WebhookId/Token` в БД при создании нового webhook.

### Этап E — наблюдение и метрики (3-5 дней, низкий риск)

E1. **Concurrency-метрики**:
   - Сколько secondsов держится семафор.
   - Очередь длиной `_queue` per character.
   - Thread-pool busy threads.
   - Retry-counters для DbUpdateConcurrencyException.

E2. **Health-check endpoint** (если перейти на `IHost`).

---

## 14. Карта тестов на конкурентность

### 14.1 ✅ Easy — pure logic

| Что | Как |
|---|---|
| `WatchDog.Validate(CachedWatchedUser)` | InternalsVisibleTo + детерминистичный счётчик в lock |
| `CachedCharacterInfo` queue methods | прямой test multi-thread с `Parallel.For` |
| `MetricsWriter.GetLastAutoMetricReport / SetLastAutoMetricReport` | trivial |
| `ActiveSearchQueriesStorage.SearchQuery` ctor | как в pure-helpers |

### 14.2 🟡 Medium — нужны mock'и + InMemoryDb

| Что | Подсадка |
|---|---|
| `MessagesHandler` cascade format lookup | InMemoryDb + mock IGuildChannel |
| Concurrent `/character edit` через два запроса | InMemoryDb + 2 параллельных task'а |
| `WatchDog.BlockUserGloballyAsync` race | InMemoryDb |

### 14.3 🔴 Hard — нужно решение через Testcontainers

| Что | Почему |
|---|---|
| Real `DbUpdateConcurrencyException` retry | EF InMemory не симулирует concurrency-tokens |
| Race на `HuntedUsers` без рефакторинга | static state |
| Background-loops с реальным timeout'ом | static `_running` блокирует тесты |
| Stress-тесты hot-path | требует `IHost` + Testcontainers Postgres |

### 14.4 Рекомендация

**Этап A1-A6 рефакторинга должен идти параллельно с написанием юнит-тестов**, потому что они уберут анти-паттерны, которые сейчас блокируют тесты (Task.WaitAll, GetResult, mutable List). После — мощный набор unit-тестов на §14.1 + §14.2 даст основу для следующих этапов.

---

## 15. Связанные точки в коде

| Файл | Что проверять |
|---|---|
| `App/CharacterEngineBot.cs:28-45` | static state и DiscordClient global |
| `App/Handlers/MessagesHandler.cs:32, 51-99, 197-392` | главный hot-path |
| `App/Repositories/CacheRepository.cs:18-22, 51` | static state и `_dbCallsSemaphore` |
| `App/Repositories/Storages/CachedCharacerInfoStorage.cs:13, 67-103` | глобальный character index + per-character queue |
| `App/Services/WatchDog.cs:22-31, 60-81, 84-119, 134-152, 201-232` | rate-limit + блокировки |
| `App/Services/BackgroundWorker.cs:22-23, 26-87` | 4 loop'а + `_running` |
| `App/Services/MetricsWriter.cs:16-26` | static `_lastAutoMetricReport` |
| `App/Helpers/Masters/IntegrationsMaster.cs:34-69` | spawn rollback (orphan webhook risk) |
| `App/Repositories/CharactersDbRepository.cs:23, 100-122, 125-146` | `_deletionLock` + DbUpdateConcurrencyException catch |
| `Modules/Modules/Universal/SakuraAiModule.cs:31-60` | мутация SakuraChatId |
| `Modules/Modules/Universal/CaiModule.cs:36-62` | мутация CaiChatId + sync upstream |
| `Modules/Modules/Chat/OpenRouterModule.cs:34-153` | свой DbContext per call + retry-bug |

---

*Документ описывает concurrency-модель по состоянию рабочей ветки `claude/analyze-repo-setup-5iQNw` (May 2026). При существенных изменениях `MessagesHandler`, `WatchDog`, `BackgroundWorker` или storage-классов — обновить.*

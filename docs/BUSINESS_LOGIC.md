# Карта бизнес-логики Character Engine

> Этот документ — «что бот делает, зачем и как». Архитектурная карта (как именно код устроен) лежит в `docs/ARCHITECTURE.md`.

## 1. Назначение продукта

Character Engine — **агрегатор LLM-платформ внутри Discord-сервера**. Он превращает текстовый канал в сцену для AI-персонажей: каждый персонаж представлен **отдельным Discord-вебхуком** (со своим именем и аватаркой), поэтому в чате он выглядит как самостоятельный пользователь, а не как ответ бота. Реально «за» вебхуком стоит выбранная LLM-платформа.

**Цель для пользователя:** ролевые сцены, чат-боты-маскоты, длинные сюжеты с несколькими персонажами одновременно. Бот — это «движок», а контент (личность, шаблон промпта, карточка) приходит из четырёх источников.

**Поддерживаемые источники** (см. `Shared/Enums.cs` + `IntegrationsHub`):

| Тип | Класс источника | Что умеет | Хранит |
|---|---|---|---|
| **CharacterAI** | `IntegrationType.CharacterAI` | поиск + чат | свой `CaiChatId` для каждого спавна (история на стороне CAI) |
| **SakuraAI** | `IntegrationType.SakuraAI` | поиск + чат + каталог для adopted-персонажей | `SakuraChatId` (история на стороне Sakura) |
| **OpenRouter** | `IntegrationType.OpenRouter` | только чат (`IsChatOnly=true`); каталог берётся из Sakura или Chub | `CharacterChatHistory` — полный лог сообщений в нашей БД, своими руками собираем историю и шлём батчем в API |
| **ChubAI** | `CharacterSourceType.ChubAI` | только источник карточек (search/info), без чата | используется как «каталог» для OpenRouter |

Из этого следует ключевая идея: **OpenRouter — единственный «прокачанный» путь**, потому что он позволяет «усыновить» персонажа из Sakura/Chub-каталога и крутить его на любой OpenRouter-модели со своим system prompt, температурой и т.д. Остальные два — это «заверни нативный платформенный чат в вебхук».

---

## 2. Доменная модель — слой «персонажа»

```
DiscordGuild ─┬─ DiscordChannel ─┬─ *SpawnedCharacter (один на вебхук)
              │                  │     ├─ CallPrefix, MessagesFormat?, ResponseDelay,
              │                  │     ├─ FreewillFactor (0..100), FreewillContextSize,
              │                  │     ├─ EnableSwipes, EnableQuotes, EnableStopButton,
              │                  │     ├─ WebhookId/WebhookToken (живой Discord-webhook)
              │                  │     └─ <платформенные поля: Sakura/Cai/OpenRouter>
              │                  │           OpenRouter "adopted":
              │                  │           ├─ AdoptedCharacterSourceType (Sakura|Chub)
              │                  │           ├─ AdoptedCharacterDefinition/Description
              │                  │           ├─ AdoptedCharacterSystemPrompt?
              │                  │           └─ Temperature/TopP/TopK/.../MaxTokens
              │                  ├─ MessagesFormat? (override для канала)
              │                  └─ SystemPrompt? (override для канала)
              ├─ MessagesFormat?  (override для сервера)
              ├─ SystemPrompt?    (override для сервера)
              ├─ *GuildIntegration (одна на тип, хранит креды/токен/email)
              └─ GuildBotManager[] (роли/юзеры с правом писать команды)

CharacterChatHistory[]  — только OpenRouter (id, role: system/user/assistant, message, ts)
HuntedUser[]            — character N "охотится" на user M (или на другого персонажа)
StoredAction[]          — отложенный/повторяемый job (сейчас только SakuraAI ensure-login)
BlockedUser/BlockedGuildUser — глобальный и серверный бан
Metric[]                — счётчик событий для отчёта
```

Поля `*SpawnedCharacter` (Sakura/Cai/OpenRouter) почти полностью пересекаются — это сознательный «полиморфизм через наследование интерфейсов». Все они реализуют `ISpawnedCharacter`, плюс свой провайдерский интерфейс (`ISakuraCharacter` / `ICaiCharacter` / `IOpenRouterCharacter`).

---

## 3. Каскад настроек (один из главных бизнес-инвариантов)

Любая «эффективная» настройка вычисляется по приоритету **character → channel → guild → BotConfig.DEFAULT_***:

- `MessagesFormat`: `SpawnedCharacter.MessagesFormat ?? DiscordChannel.MessagesFormat ?? DiscordGuild.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT` (см. `MessagesHandler` строки ~278 и `InteractionsMaster.BuildCharacterMessagesFormatDisplay`).
- `SystemPrompt` (только для OpenRouter / adopted): тот же каскад через `IAdoptedCharacter.AdoptedCharacterSystemPrompt`.
- OpenRouter-параметры генерации (`Temperature`, `TopP`, …): `OpenRouterSpawnedCharacter.X ?? OpenRouterGuildIntegration.X` (всего два уровня — без канального).

UI-слой делает каскад прозрачным: `BuildCharacterMessagesFormatDisplay` всегда показывает текущее значение **с пометкой откуда оно унаследовано** ("(inherited from channel-wide setting)" / "(default)").

---

## 4. Жизненный цикл — как админ заводит сервер

Сценарий «server onboarding» жёстко зашит в флоу команд:

1. **Бот приглашён → guild событие** → `OnJoinedGuild` создаёт `DiscordGuild`-строку и регистрирует только `/start`.
2. **Кто-то с правами `ManageGuild`/`Administrator` запускает `/start`** (`SpecialCommandsHandler.HandleStartCommandAsync`) — бот регистрирует все остальные команды + `/disable` (off-кнопку).
3. **`/integration create <Type>`** открывает модалку под платформу:
    - **SakuraAI**: спрашивает email → бот шлёт письмо через Sakura API → пользователь кликает confirm-link **в инкогнито** (явно прописано в подсказке) → фон-воркер `RunStoredActions` каждые 20 с до 25 раз пытается забрать сессию → как только подтверждено, в БД сохраняется `SakuraSessionId/SakuraRefreshToken`. Если 25 попыток (≈8 мин) истекли — `CallGiveUpFinalizer` пишет в исходный канал «время истекло, попробуй ещё раз».
    - **CharacterAI**: тоже email → Sakura-style письмо с linkом → но тут юзера **явно просят НЕ кликать**, а скопировать URL и вставить в `/integration confirm type:CharacterAI data:<url>`. Так бот забирает auth-токен сам.
    - **OpenRouter**: только модалка с API-ключом (валидация: должен начинаться с `sk-or`) + дефолтная модель (placeholder `mistralai/mistral-7b-instruct:free`).
4. **`/character spawn type query`** — поиск, либо `characterId:` для прямого спавна. Поиск возвращает 10×N карточек с пагинацией кнопками ⬆⬇⬅➡✅. Кнопками может управлять только **автор поиска или владелец сервера**.
5. На «✅ Select» бот:
   - создаёт **Discord-вебхук** в канале с именем персонажа и его аватаркой (если файл >10 МБ — пережимает MagicScaler до 600px JPEG; если URL не открылся — берёт `Settings/img/<DEFAULT_AVATAR_FILE>`),
   - сохраняет `*SpawnedCharacter` строку,
   - кэширует `DiscordWebhookClient`,
   - публикует первое сообщение (greeting) от лица вебхука с подменёнными `{{char}}/{{user}}` плейсхолдерами.
6. **Дальше пользователи разговаривают с персонажем напрямую в канале** — без слэш-команд (см. п. 5).

`/integration copy <integrationId>` — особый трюк: позволяет завести интеграцию в новом сервере, переиспользуя креды уже подтверждённого аккаунта **из другого сервера**, но только если ты **owner или manager** того сервера. Это снимает необходимость заново подтверждать email каждый раз.

`/integration re-login` — повторный email-флоу на тот же адрес (для Sakura/CAI; OpenRouter не нуждается).

`/integration remove type:X removeAssociatedCharacters:bool` — два режима:
- `true`: удалить все спавны этого типа + их вебхуки (полная зачистка),
- `false`: оставить персонажей в живых, но **обнулить им FreewillFactor** (молчат, можно вызывать только по prefix/reply).

---

## 5. Как пользователь вызывает персонажа (4 параллельных канала)

`MessagesHandler.HandleMessageAsync` на каждое guild-сообщение делает фильтр по `CacheRepository.CachedCharacters[channelId]` (исключая того персонажа, чей вебхук — это сам автор сообщения, чтобы не зацикливаться) и параллельно проверяет четыре способа триггера:

| # | Триггер | Как срабатывает | Тип вызова |
|---|---|---|---|
| 1 | **Reply на сообщение персонажа** | `socketUserMessage.ReferencedMessage.Author.Id == cachedCharacter.WebhookId` | direct |
| 2 | **Префикс в начале сообщения** | `content.StartsWith(c.CallPrefix)` | direct |
| 3 | **Freewill (RNG)** | для каждого персонажа канала бросает `random` против `FreewillFactor/100` (0–1.0). Если несколько срабатывают, выбирается тот, чьё имя/префикс упомянут в тексте; иначе — самый молчаливый | indirect |
| 4 | **Hunted users** | `HuntedUser[c]` содержит `Author.Id` → персонаж обязан ответить | direct |

Если 1 + 3 совпали по одному и тому же персонажу — берётся как direct (не дублируется). Все итоговые задачи запускаются через `Task.WaitAll` и могут отвечать в одно и то же сообщение независимо.

В ответ на indirect-вызов (freewill) и при `EnableWideContext=true`, бот **подгружает последние 20 сообщений канала** и собирает «контекстное окно» до `FreewillContextSize` символов — поверх текущего сообщения. Это превращает freewill в имитацию «персонаж читает беседу и врывается».

**Защита от петель `character ↔ character`:** если автор — webhook/bot, в `CallCharacterAsync` принудительно ставится `Math.Max(5, ResponseDelay)` секунд паузы. Это сдерживающий зазор для сцен, где двое персонажей переписываются друг с другом.

---

## 6. Шаблон сообщения (как персонаж «видит» юзера)

Это — гибкий «сериализатор» Discord-сообщения в текст для LLM. Форма по умолчанию (`BotConfig.DEFAULT_MESSAGES_FORMAT`):

```
{{ref_begin}}((In response to '{{ref_msg}}' from '{{ref_user}}')){{ref_end}}
[{{date}}] [discord_id:{{mention_hint}}] {{user}}:
{{msg}}
```

Плейсхолдеры (`MessagesHelper`):
- `{{user}}` — имя автора (пробелы → `_`)
- `{{mention_hint}}` — `<@discordId>` (LLM может вставить в ответ и юзер получит реальный пинг)
- `{{msg}}` — текст
- `{{date}}` — `hh:mm dd-MMM-yyyy`
- `{{ref_msg}}/{{ref_user}}` — текст и автор сообщения, на которое ответили (с обрезанием до 150 символов и заменой `<@...>` на `@displayName`)
- `{{ref_begin}} … {{ref_end}}` — обёртка, удаляется целиком если ответа нет (так шаблон сам себя «свернёт»)

`ValidateMessagesFormat` — обязательный `{{msg}}`; если есть `{{ref_msg}}`, то обязаны быть `{{ref_begin}} ... {{ref_end}}` вокруг него. Это валидируется на every `/character messages-format update`, `/channel ...`, `/server ...`.

Системный промпт по умолчанию (`DEFAULT_SYSTEM_PROMPT`) — развёрнутый «jailbreak»: явно разрешает NSFW и просит ИИ играть `{{CHAR}}`. Это подсказывает позиционирование: бот рассчитан на не-цензурный ролеплей (что согласуется с тем, что в `IntegrationType.SakuraAI/OpenRouter.CanNsfw() => true`).

---

## 7. NSFW и приватность поиска

- Поиск: `showNsfw:true` валиден **только в NSFW-канале** Discord (флаг канала). Иначе — `UserFriendlyException`.
- Спавн NSFW-персонажа: запрещён в не-NSFW-канале даже по прямому ID.
- В runtime: если персонаж помечен `IsNfsw=true`, то любая попытка позвать его в обычном канале возвращает purple-эмбед «can be called only in channels with age restriction» вместо ответа.

CAI (`IntegrationType.CharacterAI.CanNsfw() => false`) — единственный, где NSFW-флаг всегда `false`.

---

## 8. Управление персонажами после спавна

Все эти команды — над уже живым `*SpawnedCharacter`-ом, идентифицируется он по двум вещам: либо `CallPrefix` (например `@ai`), либо `WebhookId`. Параметр везде называется `any-identifier`.

**`/character`-группа**:
- `info` — карточка с описанием/настройками,
- `reset` — стирает контекст: для Sakura/CAI — обнуляет `*ChatId` (следующее сообщение → новый чат на стороне платформы); для OpenRouter — удаляет все строки `CharacterChatHistory`. Всегда заново шлёт greeting.
- `remove` — удаляет вебхук + строку в БД,
- `edit property newValue` — мутатор одного из: `name` (меняет вебхук), `avatar` (скачивает, ставит на вебхук), `call-prefix`, `chat-id` (привязать к существующему чату на платформе), `wide-context-max-length`, `freewill-factor` (0..100), `first-message`, `response-delay`,
- `toggle response-swipes|quotes|stop-button` — UI-фичи,
- `messages-format show|update|reset-default`, `system-prompt show|update|reset-default` — переопределение каскада,
- `openrouter-settings` — открывает модалку с JSON всех hyperparams.
- `hunted-users add|remove|show|clear-all` — добавить «жертву»: персонаж будет автоматически отвечать на её сообщения. Можно подсунуть префикс другого персонажа — тогда характер охотится на персонажа («диалог двух AI»).

**`/channel`-группа** — `messages-format`/`system-prompt show|update|reset-default` для уровня канала + `no-warn:bool` (отключить предупреждения о правах для канала), `list-characters`, `clear-characters` (массовое удаление).

**`/server`-группа** — то же `messages-format`/`system-prompt`, `no-warn` для сервера, `list-integrations`, `ignored-users` (бан внутри сервера; принимает user/userId/role), `openrouter-settings` (на уровне интеграции).

**`/managers`-команда (требует `GuildAdmin`)** — добавить юзера или роль в `GuildBotManager`. Только они могут пользоваться `/character`/`/channel`/`/server` командами. Без неё — только владелец сервера и Discord-Administrator-роли проходят `ValidateAccessLevelAsync(Manager)`.

---

## 9. Уровни доступа

`ValidationsHelper.ValidateAccessLevelAsync` определяет три уровня + bypass-список глобальных владельцев бота:

| Уровень | Кто проходит |
|---|---|
| Bypass | `BotConfig.OWNER_USERS_IDS` (список Discord-ID в `config.ini`) — всегда, везде |
| `BotAdmin` | только bypass-список (доступ к admin-командам в admin-гильдии) |
| `GuildAdmin` | владелец сервера + любая роль с permission `Administrator` |
| `Manager` | то же + любой юзер/роль из `GuildBotManager[]` |

`ValidateChannelPermissions` (атрибут на классах команд) дополнительно проверяет, что у бота в канале есть полный набор: `ViewChannel + SendMessages + AddReactions + EmbedLinks + AttachFiles + ManageWebhooks + CreatePublic/PrivateThreads + SendMessagesInThreads + ManageThreads + UseExternalEmojis`. Если чего-то нет — бросает `UserFriendlyException` с детальной диагностикой («missing perms: …», «prohibitive overwrites: …»). Если канал/сервер в `NoWarn` — пропускает молча.

---

## 10. Анти-абуз / WatchDog

`WatchDog.ValidateUser` на каждое сообщение/кнопку:

- Если юзер в `_blockedUsers` (глобал) или в `_blockedGuildUsers` (серверный игнор) → `Blocked` → silent drop.
- Иначе ведёт счётчик `InteractionsCount` в скользящем 30-секундном окне.
   - `>= USER_RATE_LIMIT - 3` (по умолчанию 12) → отправляет один warning-эмбед «слишком часто, замедлись».
   - `>= USER_RATE_LIMIT` (15) → блокирует:
     - **первый бан**: `USER_FIRST_BLOCK_MINUTES` = 30 минут,
     - **повторный**: `USER_SECOND_BLOCK_HOURS` = 24 часа.
   - В БД пишется строка `BlockedUser`, фоновый луп `RevalidateBlockedUsers` каждую минуту разбанивает по `BlockedUntil`.

Bot-admin может вручную через explicit-команды `/blockuser`/`/unblockuser` (зарегистрированы только в admin-гильдии).

---

## 11. Адмиские команды (только в admin-гильдии)

`ExplicitCommandBuilders.BuildAdminCommands()` регистрирует на гильдии с ID `BotConfig.ADMIN_GUILD_ID`:

- `shutdown` — `Environment.Exit(0)` (рестарт делает оркестратор).
- `blockuser <userId>` — глобальный бан на 1 час.
- `unblockuser <userId>`.
- `reportmetrics range-type:all-time|minutes|hours|days range:int` — сводка из `Metric[]`: serverов joined/left, новых интеграций (по типам), spawn-count, calls с уникальными character/channel/guild, уникальные юзеры. Этот же отчёт авто-постится фон-воркером `MetricsReport` каждый час в logs-канал.

`shutdown/blockGuild/unblockGuild` объявлены в enum, но `BuildAdminCommands` пока добавляет только четыре первых.

---

## 12. Ограничения, обнаруженные в коде

- **15 персонажей на канал** — это жёсткий лимит Discord на вебхуки в одном канале, не выдумка бота. Проверка идёт перед спавном (`webhooks.Count == 15`). README обещает «up to 15», что соответствует коду.
- **Длинные ответы**: если ответ от LLM > 2000 символов (Discord-лимит), `ActiveCharacterDecorator` делит на чанки и **создаёт временный thread** под исходным сообщением с заголовком `[MESSAGE LENGTH LIMIT EXCEEDED]`, пишет в нём остальные куски, архивирует thread.
- **Имя персонажа со словом "discord"**: каждое такое слово маскируется заменой латинской `o` на кириллическую `о` (`InteractionsHelper.CreateDiscordWebhookAsync` — обход Discord'овского запрета на имена с «discord»).
- **Аватар > 10 МБ** — пережимается MagicScaler-ом до 600 px JPEG.
- **`~ignore` префикс** в начале сообщения — глобальный мьют-маркер (`MessagesHandler` строка 114), все персонажи это сообщение игнорируют.
- **Очередь на одного персонажа**: каждый кэшированный персонаж имеет свой queue (`CachedCharacterInfo.QueueAddCaller/QueueIsTurnOf`). Если текущий юзер уже стоит в очереди — drop. Если ждёт > 2 минут — выходит.
- **Persisted history**: только OpenRouter (своя таблица). Sakura/CAI делегируют upstream'у — после `reset` или потери `*ChatId` контекст у них не восстановим.

---

## 13. Бизнес-сценарии «продвинутого» использования

Из вики и issue-tracker'а (полезно знать как «design intent», даже если в коде названия команд изменились):

1. **Сцена «два персонажа разговаривают друг с другом»** — каждому делается `/character hunted-users action:add anyIdentifier:A userIdOrCharacterCallPrefix:.B` и наоборот. Можно усилить через `freewill-factor:100` обоим в выделенном канале. Запускается одним `say`-сообщением (или `/character reset`, который пошлёт greeting).
2. **Возобновить старый CAI-чат**: создать персонажа, потом `/character edit property:chat-id newValue:<hist=...>` — теперь он продолжает существующий тред на стороне CAI.
3. **Один Sakura-аккаунт на N серверов**: подтвердить email единожды, дальше использовать `/integration copy <id>` на каждом следующем сервере (важно: Sakura защищается от повторных `create` — это сломает старую интеграцию).
4. **Кастомизировать одного персонажа без затрагивания каскада**: `/character system-prompt update` или `/character openrouter-settings` (модалка с JSON).
5. **Тихий «дежурный» бот**: `freewill-factor:5..10` — персонаж сам изредка комментирует разговор без запроса.
6. **Серверный «ассистент»**: `freewill-factor:0` + `hunted-users:` нужные люди — отвечает только им и только когда они пишут.

---

## 14. Расхождения «вики vs код» — что не работает как написано

Вики устарела (issue #38 это подтверждает). При работе в репо нужно опираться на код, не на вики:

| В вики написано | В коде реальность |
|---|---|
| `config.json` | `config.ini`, плюс приоритет `env.config*` |
| .NET 7 SDK, libgtk/libnss/Puppeteer | .NET 9, чистый HTTP-клиент к CAI/Sakura/OpenRouter (Puppeteer вынесен) |
| Aisekai, прямой OpenAI API | заменено на SakuraAI и OpenRouter |
| Команды `/spawn cai-character`, `/show all-characters`, `/update call-prefix`, `/admin shutdown` | сейчас сгруппированы: `/character spawn`, `/channel list-characters`, `/character edit property:call-prefix`, explicit-admin `shutdown` (не slash-group) |
| `discord_bot_manager_role`: одна авто-создаваемая роль | заменено на таблицу `GuildBotManager` + команда `/managers` |
| `default_jailbreak_prompt` | сейчас `DEFAULT_SYSTEM_PROMPT` |
| min 10 s для character↔character | в коде `Math.Max(5, ResponseDelay)` для bot/webhook авторов |
| TTS / web UI / Kobold Horde / LiteLLM | не реализовано (issues #7, #8, #31) |

---

## 15. Чего проект не делает (важно для ожиданий)

- Нет генерации изображений: в карточке персонажа всегда `Can generate images: No`. CAI-флаг `img_gen_enabled` сохраняется в БД, но не используется.
- Нет голоса/TTS, нет веб-UI, нет stream-режима ответов (они приходят целиком).
- Нет кастомных «private» персонажей без upstream — каждый персонаж обязан быть привязан к одной из четырёх платформ (вики обещала `custom-character` — в коде такого пути нет).
- Нет шеринга истории между персонажами, нет «персональностей» поверх одного персонажа.
- Нет per-user настроек — всё на уровне сервера/канала/персонажа.
- Только PostgreSQL (миграции жёстко завязаны на Npgsql).
- Slash-команды развёртываются per-guild (не глобально) — добавление новой команды доходит до пользователя сразу, без 1-часового кэша Discord.

---

## 16. Сводный «контракт» инвариантов

Если планируешь править бизнес-логику, держи в голове следующие инварианты, которые сейчас выполняются и которые легко случайно сломать:

- **Один guild = одна интеграция каждого типа** (см. `existingIntegration is not null` чек в `CreateIntegrationAsync` и `IntegrationManagementCommands.Copy`).
- **Один webhook = один SpawnedCharacter** (PK = `Id`, но в логике используется `WebhookId` как «человекочитаемый» идентификатор).
- **Каскад настроек** реализован в трёх местах (поиск формата в `MessagesHandler`, отображение в `InteractionsMaster`, fallback в `OpenRouterModule`). Любая новая настройка должна идти через все три.
- **Любая `switch`-конструкция по `IntegrationType` или enum'у должна иметь default-arm с `ArgumentOutOfRangeException`** — это проверяется компилятором (`CS8509` as error).
- **Все DB-операции в горячем пути `MessagesHandler` сериализуются через приватный `SemaphoreSlim(1,1)`** + `.GetAwaiter().GetResult()` внутри блока — ломать без переосмысления нельзя, иначе DbContext поплывёт.
- **`AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`** — DateTime везде локальный `DateTime.Now`. Введение `DateTime.UtcNow` сломает сравнения.
- **На каждое событие пишется метрика** — `MetricsWriter.Write(MetricType, EntityId?, Payload?)`. Часовой `MetricsReport` парсит payload по разделителю `:`. Изменишь формат payload — получишь кривую отчётность.

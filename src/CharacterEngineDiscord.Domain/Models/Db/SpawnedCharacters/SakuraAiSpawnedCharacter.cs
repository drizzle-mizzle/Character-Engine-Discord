using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class SakuraAiSpawnedCharacter : ISakuraCharacter, ISpawnedCharacter
{
    public SakuraAiSpawnedCharacter()
    {

    }

    public SakuraAiSpawnedCharacter(IAdoptableCharacterAdapter adoptableCharacterAdapter)
    {
        var sakuraCharacter = adoptableCharacterAdapter.GetCharacter<SakuraCharacter>();
        SakuraMessagesCount = sakuraCharacter.messageCount;
        SakuraScenario = sakuraCharacter.scenario;

        var sbDesc = new StringBuilder();

        if (sakuraCharacter.tags is JArray { Count: > 0 } tags)
        {
            sbDesc.AppendLine($"Tags: {string.Join(", ", tags)}\n");
        }

        sbDesc.AppendLine(sakuraCharacter.description.Trim('\n', ' '));
        SakuraDescription = sbDesc.ToString();

    }

    public Guid Id { get; init; } = Guid.NewGuid();

    [ForeignKey("DiscordChannel")]
    public ulong DiscordChannelId { get; set; }

    public ulong WebhookId { get; set; }

    [MaxLength(100)]
    public string WebhookToken { get; set; } = null!;

    [MaxLength(50)]
    public string CallPrefix { get; set; } = null!;

    [MaxLength(300)]
    public string? MessagesFormat { get; set; }

    public uint ResponseDelay { get; set; }
    public double FreewillFactor { get; set; }

    public uint FreewillContextSize { get; set; }
    public bool EnableSwipes { get; set; }
    public bool EnableWideContext { get; set; }
    public bool EnableQuotes { get; set; }
    public bool EnableStopButton { get; set; }
    public bool SkipNextBotMessage { get; set; }
    public ulong LastCallerDiscordUserId { get; set; }
    public ulong LastDiscordMessageId { get; set; }
    public uint MessagesSent { get; set; }
    public DateTime LastCallTime { get; set; }

    [MaxLength(100)]
    public string CharacterId { get; set; } = null!;

    [MaxLength(50)]
    public string CharacterName { get; set; } = null!;

    [MaxLength(int.MaxValue)]
    public string CharacterFirstMessage { get; set; } = null!;

    [MaxLength(500)]
    public string? CharacterImageLink { get; set; }

    [MaxLength(50)]
    public string CharacterAuthor { get; set; } = null!;

    public bool IsNfsw { get; set; }


    [MaxLength(int.MaxValue)]
    public string SakuraDescription { get; set; } = null!;

    [MaxLength(int.MaxValue)]
    public string SakuraScenario { get; set; } = null!;

    public int SakuraMessagesCount { get; set; }

    [MaxLength(00)]
    public string? SakuraChatId { get; set; }



    public DiscordChannel? DiscordChannel { get; set; }

}

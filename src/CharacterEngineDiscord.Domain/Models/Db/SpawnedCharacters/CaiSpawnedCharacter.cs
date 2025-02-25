using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class CaiSpawnedCharacter : ISpawnedCharacter, ICaiCharacter
{
    public CaiSpawnedCharacter()
    {

    }

    public CaiSpawnedCharacter(CaiCharacter caiCharacter)
    {
        CaiTitle = caiCharacter.title!;
        CaiDescription = caiCharacter.description!;
        CaiImageGenEnabled = caiCharacter.img_gen_enabled;
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


    [MaxLength(3000)]
    public string CaiTitle { get; set; } = null!;

    [MaxLength(int.MaxValue)]
    public string CaiDescription { get; set; } = null!;

    public bool CaiImageGenEnabled { get; set; }
    public int CaiChatsCount { get; set; }

    [MaxLength(40)]
    public string? CaiChatId { get; set; }

    public DiscordChannel DiscordChannel { get; set; } = null!;

}

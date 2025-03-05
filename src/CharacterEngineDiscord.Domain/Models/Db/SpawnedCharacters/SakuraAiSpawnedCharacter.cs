using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class SakuraAiSpawnedCharacter : ISpawnedCharacter, ISakuraCharacter
{
    public SakuraAiSpawnedCharacter()
    {

    }

    public SakuraAiSpawnedCharacter(SakuraCharacter sakuraCharacter)
    {
        SakuraMessagesCount = sakuraCharacter.messageCount;
        SakuraPersona = sakuraCharacter.persona;
        SakuraScenario = sakuraCharacter.scenario;

        var sbDesc = new StringBuilder();

        if (sakuraCharacter.tags is JArray { Count: > 0 } tags)
        {
            sbDesc.AppendLine($"Tags: {string.Join(", ", tags)}\n");
        }

        sbDesc.AppendLine(sakuraCharacter.description.Trim('\n', ' '));
        SakuraDescription = sbDesc.ToString();

        var messages = sakuraCharacter.exampleConversation.Where(msg => !string.IsNullOrWhiteSpace(msg.content)).ToArray();
        if (messages.Length != 0)
        {
            var lines = messages.Select(message =>
            {
                var name = message.role.StartsWith('a') // assistant
                        ? sakuraCharacter.name
                        : "User";

                return $"{name}: {message.content}";
            });

            SakuraExampleDialog = string.Join('\n', lines);
        }
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
    public string SakuraPersona { get; set; } = null!;

    [MaxLength(int.MaxValue)]
    public string SakuraScenario { get; set; } = null!;

    public int SakuraMessagesCount { get; set; }

    [MaxLength(00)]
    public string? SakuraChatId { get; set; }

    [MaxLength(int.MaxValue)]
    public string? SakuraExampleDialog { get; set; }


    public DiscordChannel DiscordChannel { get; set; } = null!;


    public string GetCharacterDefinition()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Name: {CharacterName}");

        if (!string.IsNullOrWhiteSpace(SakuraDescription))
        {
            sb.AppendLine($"[DESCRIPTION]\n{SakuraDescription.Trim('\n', ' ')}\n[DESCRIPTION_END]\n");
        }

        if (!string.IsNullOrWhiteSpace(SakuraPersona))
        {
            sb.AppendLine($"[PERSONA]\n{SakuraPersona.Trim('\n', ' ')}\n[PERSONA_END]\n");
        }

        if (!string.IsNullOrWhiteSpace(SakuraExampleDialog))
        {
            sb.AppendLine($"[EXAMPLE_DIALOG]\n{SakuraExampleDialog.Trim('\n', ' ')}\n[EXAMPLE_DIALOG_END]\n");
        }

        if (!string.IsNullOrWhiteSpace(SakuraScenario))
        {
            sb.AppendLine($"[SCENARIO]\n{SakuraScenario.Trim('\n', ' ')}\n[SCENARIO_END]\n");
        }

        return sb.ToString();
    }


    public string GetCharacterDescription()
        => SakuraDescription;


    public CharacterSourceType GetCharacterSourceType()
        => CharacterSourceType.SakuraAI;

}

using System.Text;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Clients.ChubAiClient.Models;
using CharacterEngineDiscord.Modules.Helpers;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Adapters;


public class ChubCharacterAdapter : AdoptableCharacterAdapter<ChubAiCharacter>
{
    private readonly IntegrationType _integrationType;


    public ChubCharacterAdapter(ChubAiCharacter chubAiCharacter, IntegrationType integrationType) : base(integrationType, CharacterSourceType.ChubAI)
    {
        _integrationType = integrationType;
        Character = chubAiCharacter;
    }

    protected override ChubAiCharacter Character { get; }


    public override CommonCharacter ToCommonCharacter() => new()
    {
        CharacterId = Character.FullPath,
        CharacterName = Character.Name,
        CharacterFirstMessage = Character.Definition?.First_message,
        CharacterAuthor = Character.FullPath.Contains('/') ? Character.FullPath.Split('/').First() : Character.FullPath,
        CharacterImageLink = Character.Avatar_url,
        IsNfsw = Character.Nsfw_image,
        CharacterStat = Character.StarCount.ToString(),
        IntegrationType = _integrationType,
        CharacterSourceType = GetCharacterSourceType()
    };


    public override string GetCharacterName()
        => Character.Name;


    public static string GetCharacterLink(string fullPath)
    {
        var id = fullPath.Trim(' ', '/');
        return $"https://chub.ai/characters/{id}";
    }

    public override string GetCharacterLink()
        => GetCharacterLink(Character.FullPath);


    public static string GetAuthorLink(string fullPath)
    {
        var username = fullPath.Split('/').First();
        return $"https://chub.ai/users/{username}";
    }

    public override string GetAuthorLink()
        => GetAuthorLink(Character.FullPath);


    public override string GetCharacterDescription()
    {
        var taglineSb = new StringBuilder();

        if (!string.IsNullOrEmpty(Character.Definition.Tavern_personality))
        {
            taglineSb.AppendLine(Character.Definition.Tavern_personality);
            taglineSb.AppendLine();
        }

        if (!string.IsNullOrEmpty(Character.Tagline))
        {
            taglineSb.AppendLine(Character.Tagline);
        }

        var description = $"{Character.Definition.Description.Replace("\n---\n", string.Empty)}";
        return Templates.BuildCharacterDescription(Character.Name, taglineSb.ToString(), description, Character.Definition.Scenario);
    }


    public override string GetCharacterDefinition()
    {
        var personalitySb = new StringBuilder();

        if (!string.IsNullOrEmpty(Character.Definition.Tavern_personality))
        {
            personalitySb.AppendLine(Character.Definition.Tavern_personality);
        }

        personalitySb.AppendLine(Character.Definition.Personality);

        return Templates.BuildCharacterDefinition(Character.Name, personalitySb.ToString(), Character.Definition.Scenario, Character.Definition.Example_dialogs);
    }
}

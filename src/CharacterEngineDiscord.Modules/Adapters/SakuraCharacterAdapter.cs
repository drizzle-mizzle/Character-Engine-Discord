using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Helpers;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;
using CharacterEngineDiscord.Shared.Models;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Adapters;


public class SakuraCharacterAdapter : AdoptableCharacterAdapter<SakuraCharacter>
{
    public SakuraCharacterAdapter(SakuraCharacter sakuraCharacter)
        : base(IntegrationType.SakuraAI, CharacterSourceType.SakuraAI)
    {
        Character = sakuraCharacter;
    }


    protected override SakuraCharacter Character { get; }


    public override CommonCharacter ToCommonCharacter() => new()
    {
        CharacterId = Character.id,
        CharacterName = Character.name,
        CharacterFirstMessage = Character.firstMessage,
        CharacterAuthor = Character.creatorUsername,
        CharacterImageLink = Character.imageUri,
        CharacterStat = Character.messageCount.ToString(),
        IntegrationType = GetIntegrationType(),
        CharacterSourceType = GetCharacterSourceType(),
        IsNfsw = Character.nsfw,
    };


    public override string GetCharacterName() => Character.name;


    public static string GetCharacterDescription(ISakuraCharacter sakuraCharacter)
        => Templates.BuildCharacterDescription(sakuraCharacter.CharacterName, null, sakuraCharacter.SakuraDescription, sakuraCharacter.SakuraScenario);


    public override string GetCharacterDescription()
    {
        var tagline = $"[ {string.Join(", ", Character.tags)} ]";
        return Templates.BuildCharacterDescription(Character.name, tagline, Character.description, Character.scenario);
    }


    public override string GetCharacterDefinition()
    {
        var exampleDialog = Character.exampleConversation.Select(msg => (msg.role, msg.content)).ToArray();

        return Templates.BuildCharacterDefinition(Character.name, Character.persona, Character.scenario, exampleDialog);
    }


    public static string GetCharacterLink(string characterId) =>
        $"https://www.sakura.fm/chat/{characterId}";

    public override string GetCharacterLink()
        => GetCharacterLink(Character.id);


    public static string GetAuthorLink(string authorUsername)
        => $"https://www.sakura.fm/user/{authorUsername}";

    public override string GetAuthorLink()
        => GetAuthorLink(Character.creatorUsername);

}

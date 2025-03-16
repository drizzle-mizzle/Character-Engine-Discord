using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Helpers;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Adapters;


public class CaiCharacterAdapter : CharacterAdapter<CaiCharacter>
{
    public CaiCharacterAdapter(CaiCharacter caiCharacter) : base(IntegrationType.CharacterAI)
    {
        Character = caiCharacter;
    }


    protected override CaiCharacter Character { get; }


    public override CommonCharacter ToCommonCharacter() => new()
    {
        CharacterId = Character.external_id!,
        CharacterName = Character.participant__name!,
        CharacterFirstMessage = Character.greeting!,
        CharacterAuthor = Character.user__username!,
        CharacterImageLink = $"https://characterai.io/i/200/static/avatars/{Character.avatar_file_name}?webp=true&anim=0",
        CharacterStat = Character.participant__num_interactions.ToString(),
        IntegrationType = IntegrationType.CharacterAI,
        CharacterSourceType = null,
        IsNfsw = false,
    };


    public override string GetCharacterName()
        => Character.external_id!;


    public static string GetCharacterLink(string externalId)
        => $"https://character.ai/chat/{externalId}";


    public override string GetCharacterLink()
        => GetCharacterLink(Character.external_id!);


    public static string GetAuthorLink(string authorUsername)
        => $"https://character.ai/profile/{authorUsername}";

    public override string GetAuthorLink()
        => GetAuthorLink(Character.user__username!);


    public static string GetCharacterDescription(ICaiCharacter caiCharacter)
        => Templates.BuildCharacterDescription(caiCharacter.CharacterName, caiCharacter.CaiTitle, caiCharacter.CaiDescription, null);

    public override string GetCharacterDescription()
        => Templates.BuildCharacterDescription(Character.participant__name!, Character.title!, Character.description!, null);

}

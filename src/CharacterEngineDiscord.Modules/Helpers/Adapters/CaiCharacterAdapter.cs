using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;

namespace CharacterEngineDiscord.Modules.Helpers.Adapters;


public class CaiCharacterAdapter : CharacterAdapter<CaiCharacter>
{
    public CaiCharacterAdapter(CaiCharacter caiCharacter)
    {
        Value = caiCharacter;
    }


    public override CaiCharacter Value { get; }


    public override CommonCharacter ToCommonCharacter() => new()
    {
        CharacterId = Value.external_id!,
        CharacterName = Value.participant__name!,
        CharacterFirstMessage = Value.greeting!,
        CharacterAuthor = Value.user__username!,
        CharacterImageLink = $"https://characterai.io/i/200/static/avatars/{Value.avatar_file_name}?webp=true&anim=0",
        CharacterStat = Value.participant__num_interactions.ToString(),
        IntegrationType = IntegrationType.CharacterAI,
        CharacterSourceType = null,
        IsNfsw = false,
    };


    public override IntegrationType GetIntegrationType()
        => IntegrationType.CharacterAI;

}

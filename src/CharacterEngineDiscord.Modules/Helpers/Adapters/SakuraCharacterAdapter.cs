using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Modules.Abstractions;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Helpers.Adapters;


public class SakuraCharacterAdapter : CharacterAdapter<SakuraCharacter>
{

    public SakuraCharacterAdapter(SakuraCharacter sakuraCharacter)
    {
        Value = sakuraCharacter;
    }


    public override SakuraCharacter Value { get; }


    public override CommonCharacter ToCommonCharacter() => new()
    {
        CharacterId = Value.id,
        CharacterName = Value.name,
        CharacterFirstMessage = Value.firstMessage,
        CharacterAuthor = Value.creatorUsername,
        CharacterImageLink = Value.imageUri,
        CharacterStat = Value.messageCount.ToString(),
        IntegrationType = IntegrationType.SakuraAI,
        CharacterSourceType = CharacterSourceType.SakuraAI,
        IsNfsw = Value.nsfw,

    };


    public override IntegrationType GetIntegrationType()
        => IntegrationType.SakuraAI;


    public override IReusableCharacter ToReusableCharacter()
        => new SakuraAiSpawnedCharacter(Value);

}

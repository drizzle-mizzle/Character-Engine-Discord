using CharacterEngineDiscord.Domain.Models.Abstractions;

namespace CharacterEngineDiscord.Domain.Models.Common;


public class CommonCharacter : ICharacter
{
    public required string CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required string CharacterFirstMessage { get; set; }
    public required string CharacterAuthor { get; set; }
    public required string? CharacterImageLink { get; set; }

    public required bool IsNfsw { get; set; }
    public required string? CharacterStat { get; init; }

    public required IntegrationType IntegrationType { get; set; }
    public required CharacterSourceType? CharacterSourceType { get; set; }
}

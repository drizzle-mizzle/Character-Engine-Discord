using CharacterEngine.Models.Abstractions;
using SakuraAi.Models.Common;

namespace CharacterEngine.Models;


public record ModalData(Guid Id, Enums.ModalActionType ActionType, string Data);

public record SakuraAiEnsureLoginData(SignInAttempt SignInAttempt, ulong ChannelId, ulong UserId);

public record CommonCharacter : ICommonCharacter
{
    public required string CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required string CharacterDesc { get; set; }
    public required string CharacterFirstMessage { get; set; }
    public string? CharacterImageLink { get; set; }
    public required int Stat { get; set; }
    public required string Author { get; set; }
}

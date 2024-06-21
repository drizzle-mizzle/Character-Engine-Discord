using SakuraAi.Models.Common;

namespace CharacterEngine.Models;


public record ModalData(Guid Id, Enums.ModalActionType ActionType, string Data);

public record SakuraAiEnsureLoginData(SignInAttempt SignInAttempt, ulong ChannelId, ulong UserId);

namespace CharacterEngineDiscord.Modules.Abstractions.Base;


public abstract class ModuleBase<TClient> where TClient : new()
{
    protected readonly TClient _client = new();
}

using CharacterEngineDiscord.Models;

namespace CharacterEngine.App.Repositories.Abstractions;


public abstract class RepositoryBase : IDisposable, IAsyncDisposable
{
    protected readonly AppDbContext DB;


    protected RepositoryBase(AppDbContext db)
    {
        DB = db;
    }


    public void Dispose()
    {
        DB.Dispose();
    }


    public async ValueTask DisposeAsync()
    {
        await DB.DisposeAsync();
    }
}

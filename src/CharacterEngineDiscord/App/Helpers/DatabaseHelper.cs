namespace CharacterEngine.App.Helpers;


public static class DatabaseHelper
{
    public static string DbConnectionString = $"Host=db;Port=5432;Database={Environment.GetEnvironmentVariable("POSTGRES_DB")};"
                                            + $"Username={Environment.GetEnvironmentVariable("POSTGRES_USER")};"
                                            + $"Password={Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")}";
}

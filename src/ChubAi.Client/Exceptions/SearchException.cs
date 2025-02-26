namespace ChubAi.Client.Exceptions;


public class SearchException(string query, Exception? OriginalException = null)
        : Exception($"Failed to perform search for query \"{query}\"");

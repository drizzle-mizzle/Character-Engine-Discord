using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ChubAi.Client;

public class ChubAiClient : IDisposable
{
    private readonly HttpClient HTTP_CLIENT = new HttpClient();


    private readonly JsonSerializerSettings _defaultSettings;
    private readonly JsonSerializer _defaultSerializer;


    public ChubAiClient()
    {
        HTTP_CLIENT = new HttpClient();

        _defaultSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        _defaultSerializer = JsonSerializer.Create(_defaultSettings);
    }


    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }

        HTTP_CLIENT.Dispose();

        _disposedValue = true;
    }


    private bool _disposedValue;

    #endregion Dispose
}

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        // Simply checks whether image is avalable.
        // (cAI is used to have broken undownloadable images or sometimes it's just
        //  takes eternity for it to upload one on server, but image url is provided in advance)
        public static async Task<bool> TryGetImageAsync(string url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            for (int i = 0; i < 10; i++)
                if ((await httpClient.GetAsync(url).ConfigureAwait(false)).IsSuccessStatusCode)
                    return true;
                else
                    await Task.Delay(3000);

            return false;
        }

        public static async Task<Stream?> TryDownloadImgAsync(string? url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            for (int i = 0; i < 10; i++)
            {
                try {
                    var response = await httpClient.GetAsync(url).ConfigureAwait(false);
                    return await response.Content.ReadAsStreamAsync();
                }
                catch { await Task.Delay(3000); }
            }

            return null;
        }
    }
}

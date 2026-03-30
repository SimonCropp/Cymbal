using System.Net;
using System.Net.Http;

public static class SymbolDownloader
{
    public static (List<string> missingSymbols, List<string> foundSymbols) Run(
        string? cacheDirectory,
        List<string> toDownload,
        string[] symbolServers,
        CancellationToken cancellationToken = default) =>
        RunAsync(cacheDirectory, toDownload, symbolServers, cancellationToken)
            .GetAwaiter()
            .GetResult();

    static async Task<(List<string> missingSymbols, List<string> foundSymbols)> RunAsync(
        string? cacheDirectory,
        List<string> toDownload,
        string[] symbolServers,
        CancellationToken cancellationToken)
    {
        var missingSymbols = new List<string>();
        var foundSymbols = new List<string>();

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(4)
        };

        foreach (var assemblyPath in toDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = GetPdbKey(assemblyPath);
            if (key == null)
            {
                missingSymbols.Add(Path.GetFileName(assemblyPath));
                continue;
            }

            var targetPath = Path.ChangeExtension(assemblyPath, ".pdb");

            // Try cache first
            if (cacheDirectory != null)
            {
                var cachePath = GetCachePath(cacheDirectory, key);
                if (File.Exists(cachePath))
                {
                    File.Copy(cachePath, targetPath, overwrite: true);
                    foundSymbols.Add(targetPath);
                    continue;
                }
            }

            // Try symbol servers
            byte[]? pdbBytes = null;
            foreach (var server in symbolServers)
            {
                pdbBytes = await TryDownloadAsync(client, server, key, cancellationToken).ConfigureAwait(false);
                if (pdbBytes != null)
                {
                    break;
                }
            }

            if (pdbBytes != null)
            {
                File.WriteAllBytes(targetPath, pdbBytes);
                foundSymbols.Add(targetPath);

                if (cacheDirectory != null)
                {
                    WriteToCache(cacheDirectory, key, pdbBytes);
                }
            }
            else
            {
                missingSymbols.Add(Path.GetFileName(assemblyPath));
            }
        }

        return (missingSymbols, foundSymbols);
    }

    static string? GetPdbKey(string assemblyPath)
    {
        try
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new PEReader(stream);

            foreach (var entry in reader.ReadDebugDirectory())
            {
                if (entry.Type != DebugDirectoryEntryType.CodeView)
                {
                    continue;
                }

                var codeView = reader.ReadCodeViewDebugDirectoryData(entry);
                if (string.IsNullOrEmpty(codeView.Path))
                {
                    continue;
                }

                var pdbFileName = Path.GetFileName(codeView.Path).ToLowerInvariant();
                var isPortable = entry.MinorVersion == 0x504d;

                var id = isPortable
                    ? codeView.Guid.ToString("N") + "FFFFFFFF"
                    : codeView.Guid.ToString("N") + codeView.Age.ToString("x");

                return $"{pdbFileName}/{id}/{pdbFileName}";
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException or IOException)
        {
        }

        return null;
    }

    static async Task<byte[]?> TryDownloadAsync(
        HttpClient client,
        string server,
        string key,
        CancellationToken token)
    {
        var escapedKey = string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
        var baseUri = new Uri(server.TrimEnd('/') + "/");

        if (!Uri.TryCreate(baseUri, escapedKey, out var requestUri))
        {
            return null;
        }

        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(requestUri, token).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!IsRetryable(response.StatusCode))
                {
                    return null;
                }
            }
            catch (HttpRequestException)
            {
                if (attempt >= maxRetries)
                {
                    return null;
                }
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt + 1) * 100), token).ConfigureAwait(false);
            }
        }

        return null;
    }

    static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    static string GetCachePath(string cacheDirectory, string key) =>
        Path.Combine(cacheDirectory, key.Replace('/', Path.DirectorySeparatorChar));

    static void WriteToCache(string cacheDirectory, string key, byte[] data)
    {
        try
        {
            var cachePath = GetCachePath(cacheDirectory, key);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllBytes(cachePath, data);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }
    }
}

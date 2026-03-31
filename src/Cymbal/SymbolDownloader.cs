using System.Net.Sockets;

public static class SymbolDownloader
{
    public static (List<string> missingSymbols, List<string> foundSymbols) Run(
        string? cacheDirectory,
        List<string> toDownload,
        string[] symbolServers,
        Cancel cancel = default) =>
        RunAsync(cacheDirectory, toDownload, symbolServers, cancel)
            .GetAwaiter()
            .GetResult();

    static async Task<(List<string> missingSymbols, List<string> foundSymbols)> RunAsync(
        string? cacheDirectory,
        List<string> toDownload,
        string[] symbolServers,
        Cancel cancel)
    {
        var missingSymbols = new List<string>();
        var foundSymbols = new List<string>();
        var failedServers = new HashSet<string>();

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(4)
        };

        foreach (var assemblyPath in toDownload)
        {
            cancel.ThrowIfCancellationRequested();

            var pdbInfo = GetPdbInfo(assemblyPath);
            if (pdbInfo == null)
            {
                continue;
            }

            var targetPath = Path.ChangeExtension(assemblyPath, ".pdb");

            // Try cache first
            if (cacheDirectory != null)
            {
                var cachePath = GetCachePath(cacheDirectory, pdbInfo.Key);
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
                if (failedServers.Contains(server))
                {
                    continue;
                }

                var result = await TryDownloadAsync(client, server, pdbInfo, cancel).ConfigureAwait(false);
                if (result.StickyFailure)
                {
                    failedServers.Add(server);
                }

                if (result.Data != null)
                {
                    pdbBytes = result.Data;
                    break;
                }
            }

            if (pdbBytes != null)
            {
                File.WriteAllBytes(targetPath, pdbBytes);
                foundSymbols.Add(targetPath);

                if (cacheDirectory != null)
                {
                    WriteToCache(cacheDirectory, pdbInfo.Key, pdbBytes);
                }
            }
            else
            {
                if (!missingSymbols.Contains(pdbInfo.PdbFileName))
                {
                    missingSymbols.Add(pdbInfo.PdbFileName);
                }
            }
        }

        return (missingSymbols, foundSymbols);
    }

    record PdbInfo(string Key, string PdbFileName, string? Checksum);

    record DownloadResult(byte[]? Data, bool StickyFailure);

    static PdbInfo? GetPdbInfo(string assemblyPath)
    {
        try
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new PEReader(stream);
            var entries = reader.ReadDebugDirectory();

            string? key = null;
            string? pdbFileName = null;
            string? checksum = null;

            foreach (var entry in entries)
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView && key == null)
                {
                    var codeView = reader.ReadCodeViewDebugDirectoryData(entry);
                    if (string.IsNullOrEmpty(codeView.Path))
                    {
                        continue;
                    }

                    pdbFileName = Path.GetFileName(codeView.Path);
                    var pdbFileNameLower = pdbFileName.ToLowerInvariant();
                    var isPortable = entry.MinorVersion == 0x504d;

                    var id = isPortable
                        ? codeView.Guid.ToString("N") + "FFFFFFFF"
                        : codeView.Guid.ToString("N") + codeView.Age.ToString("x");

                    key = $"{pdbFileNameLower}/{id}/{pdbFileNameLower}";
                }
                else if (entry.Type == DebugDirectoryEntryType.PdbChecksum && checksum == null)
                {
                    var cs = reader.ReadPdbChecksumDebugDirectoryData(entry);
                    var hex = BitConverter.ToString(cs.Checksum.ToArray()).Replace("-", "").ToLowerInvariant();
                    checksum = $"{cs.AlgorithmName}:{hex}";
                }
            }

            return key == null ? null : new PdbInfo(key, pdbFileName!, checksum);
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException or IOException)
        {
        }

        return null;
    }

    static async Task<DownloadResult> TryDownloadAsync(
        HttpClient client,
        string server,
        PdbInfo pdbInfo,
        Cancel cancel)
    {
        var escapedKey = string.Join("/", pdbInfo.Key.Split('/').Select(Uri.EscapeDataString));
        var baseUri = new Uri(server.TrimEnd('/') + "/");

        if (!Uri.TryCreate(baseUri, escapedKey, out var requestUri))
        {
            return new(null, false);
        }

        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                if (pdbInfo.Checksum != null)
                {
                    request.Headers.Add("SymbolChecksum", pdbInfo.Checksum);
                }

                using var response = await client.SendAsync(request, cancel).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return new(data, false);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new(null, false);
                }

                if (!IsRetryableStatus(response.StatusCode))
                {
                    return new(null, StickyFailure: true);
                }
            }
            catch (HttpRequestException ex)
            {
                if (!IsRetryableException(ex))
                {
                    return new(null, StickyFailure: true);
                }

                if (attempt >= maxRetries)
                {
                    return new(null, false);
                }
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt + 1) * 100), cancel).ConfigureAwait(false);
            }
        }

        return new(null, false);
    }

    static bool IsRetryableStatus(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    static bool IsRetryableException(HttpRequestException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is SocketException se)
            {
                return se.SocketErrorCode is
                    SocketError.ConnectionReset or
                    SocketError.ConnectionAborted or
                    SocketError.Shutdown or
                    SocketError.TimedOut or
                    SocketError.TryAgain;
            }
        }

        return false;
    }

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

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

            // Try symbol servers — stream directly to cache (or target if no cache)
            string? downloadPath;
            if (cacheDirectory != null)
            {
                downloadPath = GetCachePath(cacheDirectory, pdbInfo.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);
            }
            else
            {
                downloadPath = targetPath;
            }

            var downloaded = false;
            foreach (var server in symbolServers)
            {
                if (failedServers.Contains(server))
                {
                    continue;
                }

                var result = await TryDownloadAsync(client, server, pdbInfo, downloadPath, cancel).ConfigureAwait(false);
                if (result.StickyFailure)
                {
                    failedServers.Add(server);
                }

                if (result.Success)
                {
                    downloaded = true;
                    break;
                }
            }

            if (downloaded)
            {
                if (downloadPath != targetPath)
                {
                    File.Copy(downloadPath, targetPath, overwrite: true);
                }

                foundSymbols.Add(targetPath);
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

    record DownloadResult(bool Success, bool StickyFailure);

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
        string destinationPath,
        Cancel cancel)
    {
        var escapedKey = string.Join("/", pdbInfo.Key.Split('/').Select(Uri.EscapeDataString));
        var baseUri = new Uri(server.TrimEnd('/') + "/");

        if (!Uri.TryCreate(baseUri, escapedKey, out var requestUri))
        {
            return new(false, false);
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

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                    return new(true, false);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new(false, false);
                }

                if (!IsRetryableStatus(response.StatusCode))
                {
                    return new(false, StickyFailure: true);
                }
            }
            catch (HttpRequestException ex)
            {
                if (!IsRetryableException(ex))
                {
                    return new(false, StickyFailure: true);
                }

                if (attempt >= maxRetries)
                {
                    return new(false, false);
                }
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt + 1) * 100), cancel).ConfigureAwait(false);
            }
        }

        return new(false, false);
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

}

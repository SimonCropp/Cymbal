using System.Net.Sockets;
using Replicant;

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

        var effectiveCacheDir = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "Cymbal");
        using var httpCache = new HttpCache(
            effectiveCacheDir,
            new HttpClient { Timeout = TimeSpan.FromMinutes(4) },
            maxRetries: 3);

        foreach (var assemblyPath in toDownload)
        {
            cancel.ThrowIfCancellationRequested();

            var pdbInfo = GetPdbInfo(assemblyPath);
            if (pdbInfo == null)
            {
                continue;
            }

            var targetPath = Path.ChangeExtension(assemblyPath, ".pdb");

            var downloaded = false;
            foreach (var server in symbolServers)
            {
                if (failedServers.Contains(server))
                {
                    continue;
                }

                var url = BuildUrl(server, pdbInfo);
                if (url == null)
                {
                    continue;
                }

                try
                {
                    using var response = await httpCache.ResponseAsync(
                        url,
                        modifyRequest: request =>
                        {
                            if (pdbInfo.Checksum != null)
                            {
                                request.Headers.Add("SymbolChecksum", pdbInfo.Checksum);
                            }
                        },
                        cancel: cancel);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                        downloaded = true;
                        break;
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        continue;
                    }

                    if (!IsRetryableStatus(response.StatusCode))
                    {
                        failedServers.Add(server);
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (!IsRetryableException(ex))
                    {
                        failedServers.Add(server);
                    }
                }
            }

            if (downloaded)
            {
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

    static string? BuildUrl(string server, PdbInfo pdbInfo)
    {
        var escapedKey = string.Join("/", pdbInfo.Key.Split('/').Select(Uri.EscapeDataString));
        var baseUri = new Uri(server.TrimEnd('/') + "/");
        if (!Uri.TryCreate(baseUri, escapedKey, out var requestUri))
        {
            return null;
        }

        return requestUri.ToString();
    }

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
}

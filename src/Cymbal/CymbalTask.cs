using Task = Microsoft.Build.Utilities.Task;

public class CymbalTask :
    Task,
    ICancelableTask
{
    const string msdlSymbolServer = "https://msdl.microsoft.com/download/symbols/";
    const string nugetSymbolServer = "https://symbols.nuget.org/download/symbols";

    [Required]
    public string PublishDirectory { get; set; } = null!;

    public string? CacheDirectory { get; set; }

    public string[]? SymbolServers { get; set; }

    public override bool Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            InnerExecute();
            return true;
        }
        catch (Error exception)
        {
            Log.LogError($"Cymbal: {exception.Message}");
            return false;
        }
        finally
        {
            Log.LogMessageFromText($"Finished Cymbal {stopwatch.ElapsedMilliseconds}ms", MessageImportance.High);
        }
    }

    void InnerExecute()
    {
        string? cacheDirectory;
        var environmentCacheDirectory = Environment.GetEnvironmentVariable("CymbalCacheDirectory");
        if (CacheDirectory == null)
        {
            cacheDirectory = environmentCacheDirectory;
        }
        else
        {
            cacheDirectory = Path.GetFullPath(CacheDirectory);
        }

        if (cacheDirectory != null)
        {
            Directory.CreateDirectory(cacheDirectory);
        }

        var fullPublishPath = Path.GetFullPath(PublishDirectory);
        var inputs =
            $"""
             PublishDir: {fullPublishPath}
             CymbalCacheDirectory environment variable: {environmentCacheDirectory}
             CymbalCacheDirectory MsBuild property: {CacheDirectory}
             Resolved CacheDirectory: {cacheDirectory}
             """;
        Log.LogMessageFromText(inputs, MessageImportance.High);

        var (hasPdb, isEmbedded, toDownload) = GetFiles(fullPublishPath);

        if (hasPdb.Count != 0)
        {
            Log.LogMessageFromText($"Skipped assemblies with existing pdbs:{ListToIndented(hasPdb)}", MessageImportance.Normal);
        }

        if (isEmbedded.Count != 0)
        {
            Log.LogMessageFromText($"Skipped assemblies with embedded symbols:{ListToIndented(isEmbedded)}", MessageImportance.Normal);
        }

        if (toDownload.Count == 0)
        {
            Log.LogMessageFromText("No assemblies found to process", MessageImportance.Normal);
            return;
        }

        Log.LogMessageFromText($"Assemblies to process:{ListToIndented(toDownload)}", MessageImportance.Normal);

        var symbolServers = SymbolServers ?? defaultSymbolServers;
        Log.LogMessageFromText($"Symbol servers used:{ListToIndented(symbolServers)}", MessageImportance.Normal);

        var (missingSymbols, foundSymbols) = SymbolDownloader.Run(cacheDirectory, toDownload, symbolServers);

        if (foundSymbols.Count != 0)
        {
            Log.LogMessageFromText($"Symbols written:{ListToIndented(foundSymbols)}", MessageImportance.High);
        }

        if (missingSymbols.Count != 0)
        {
            Log.LogMessageFromText($"Missing Symbols:{ListToIndented(missingSymbols)}", MessageImportance.High);
        }
    }

    static string[] defaultSymbolServers =
    [
        nugetSymbolServer,
        msdlSymbolServer
    ];

    static string ListToIndented(IEnumerable<string> toDownload) =>
        $"{Environment.NewLine}\t{string.Join($"{Environment.NewLine}\t", toDownload)}";

    static (List<string> hasPdb, List<string> isEmbedded, List<string> toDownload) GetFiles(string fullPublishPath)
    {
        var toDownload = new List<string>();
        var isEmbedded = new List<string>();
        var hasPdb = new List<string>();
        foreach (var assemblyPath in Directory.EnumerateFiles(fullPublishPath, "*.dll", SearchOption.AllDirectories))
        {
            var symbolPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(symbolPath))
            {
                hasPdb.Add(assemblyPath);
                continue;
            }

            if (SymbolChecker.HasEmbedded(assemblyPath))
            {
                isEmbedded.Add(assemblyPath);
                continue;
            }

            toDownload.Add(assemblyPath);
        }

        return (hasPdb, isEmbedded, toDownload);
    }

    public void Cancel()
    {
    }
}
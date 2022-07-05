using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

public class CymbalTask :
    Task,
    ICancelableTask
{
    [Required]
    public string PublishDirectory { get; set; } = null!;

    public string? CacheDirectory { get; set; }

    public override bool Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            InnerExecute();
            return true;
        }
        catch (ErrorException exception)
        {
            Log.LogError($"Cymbal: {exception}");
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
        var inputs = $@"
PublishDir: {fullPublishPath}
CymbalCacheDirectory environment variable: {environmentCacheDirectory}
CymbalCacheDirectory MsBuild property: {CacheDirectory}
Resolved CacheDirectory: {cacheDirectory}
";
        Log.LogMessageFromText(inputs, MessageImportance.High);

        var (hasPdb, isEmbedded, toDownload) = GetFiles(fullPublishPath);

        if (hasPdb.Any())
        {
            Log.LogMessageFromText($"Skipped assemblies with existing pdbs: {ListToIndented(hasPdb)}", MessageImportance.Normal);
        }

        if (isEmbedded.Any())
        {
            Log.LogMessageFromText($"Skipped assemblies with embedded symbols: {ListToIndented(isEmbedded)}", MessageImportance.Normal);
        }

        if (!toDownload.Any())
        {
            Log.LogMessageFromText("No assemblies found to process", MessageImportance.Normal);
            return;
        }

        Log.LogMessageFromText($"Assemblies to process: {ListToIndented(toDownload)}", MessageImportance.Normal);

        var arguments = "tool run dotnet-symbol --server-path https://symbols.nuget.org/download/symbols --server-path https://msdl.microsoft.com/download/symbols/ ";

        if (cacheDirectory != null)
        {
            arguments += $"--cache-directory {cacheDirectory} ";
        }

        arguments += string.Join(" ", toDownload);

        var result = ProcessRunner.Execute("dotnet", arguments);

        var builder = new StringBuilder();
        foreach (var line in result)
        {
            var scrubbedLine = line.Replace("ERROR: Not Found: ", "Not Found: ");
            builder.AppendLine($"\t{scrubbedLine}");
        }

        Log.LogMessageFromText($"dotnet-symbol result:{Environment.NewLine}{builder}", MessageImportance.High);
    }

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
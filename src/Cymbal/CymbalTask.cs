using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

public class CymbalTask :
    Task,
    ICancelableTask
{
    [Required]
    public string PublishDir { get; set; } = null!;

    [Required]
    public ITaskItem[] TargetOutputs { get; set; } = null!;

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
            Log.LogMessageFromText($"Finished Cymbal {stopwatch.ElapsedMilliseconds}ms", MessageImportance.Normal);
        }
    }

    void InnerExecute()
    {
        var fullPublishPath = Path.GetFullPath(PublishDir);
        var inputs = $@"
PublishDir: {fullPublishPath}
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

        var result = ProcessRunner.Execute("dotnet-symbol", string.Join(" ", toDownload));

        var builder = new StringBuilder();
        foreach (var line in result)
        {
            builder.AppendLine($"\t{line}");
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
        foreach (var assemblyPath in Directory.EnumerateFiles(fullPublishPath, "*.dll"))
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
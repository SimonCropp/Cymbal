using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Cymbal;

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

        var files = GetFiles(fullPublishPath).ToList();

        if (!files.Any())
        {
            Log.LogMessageFromText("No assemblies found to process", MessageImportance.Normal);
            return;
        }

        Log.LogMessageFromText($"Files: {string.Join($"\t{Environment.NewLine}",files)}", MessageImportance.High);

        var result = ProcessRunner.Execute("dotnet-symbol", string.Join(" ",files));

        Log.LogMessageFromText($"dotnet-symbol result: {result}", MessageImportance.High);
    }

    static IEnumerable<string> GetFiles(string fullPublishPath)
    {
        foreach (var assemblyPath in Directory.EnumerateFiles(fullPublishPath, "*.dll"))
        {
            var symbolPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (!File.Exists(symbolPath))
            {
                yield return assemblyPath;
            }
        }
    }

    public void Cancel()
    {
    }
}
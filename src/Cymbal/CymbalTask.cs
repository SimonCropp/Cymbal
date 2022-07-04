using System.Diagnostics;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Cymbal;

public class CymbalTask :
    Task,
    ICancelableTask
{
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
        var outputs = TargetOutputs
            .Select(x => x.ItemSpec)
            .ToList();

        foreach (var output in outputs)
        {
            Log.LogWarning("d");
        }
    }

    public void Cancel()
    {
    }
}
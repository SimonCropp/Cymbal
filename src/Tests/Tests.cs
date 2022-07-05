using CliWrap;
using CliWrap.Buffered;

[UsesVerify]
public class Tests
{
    [Theory]
    [MemberData(nameof(GetData))]
    public async Task RunTask(bool environmentCache, bool propertyCache)
    {
        var solutionDir = AttributeReader.GetSolutionDirectory();
        var sampleAppPath = Path.Combine(solutionDir, "SampleApp");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await RunDotnet("build --configuration IncludeTask");

        var environmentVariables = new Dictionary<string, string?>();
        var cacheDirectory = Path.Combine(solutionDir, "Cache");
        if (environmentCache)
        {
            environmentVariables.Add("CymbalCacheDirectory", cacheDirectory);
        }

        var arguments = "publish --configuration IncludeTask --no-build --no-restore --verbosity normal";
        if (propertyCache)
        {
            arguments += $" -p:CymbalCacheDirectory={cacheDirectory}";
        }

        var publishResult = await Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(sampleAppPath)
            .WithValidation(CommandResultValidation.None).WithEnvironmentVariables(environmentVariables)
            .ExecuteBufferedAsync();

        try
        {
            if (publishResult.StandardError.Length > 0)
            {
                throw new(publishResult.StandardError);
            }

            if (publishResult.StandardOutput.Contains("error"))
            {
                throw new(publishResult.StandardOutput.Replace(solutionDir, ""));
            }

            var appPath = Path.Combine(solutionDir, "SampleApp/bin/IncludeTask/SampleApp.dll");
            var runResult = await RunDotnet(appPath);

            await Verify(
                    new
                    {
                        buildOutput = publishResult.StandardOutput,
                        consoleOutput = runResult.StandardOutput,
                        consoleError = runResult.StandardError
                    })
                .UseParameters(environmentCache, propertyCache)
                .ScrubLinesWithReplace(line => line.Replace('\\', '/'))
                .ScrubLinesContaining(
                    "Build started",
                    "Time Elapsed",
                    "Finished Cymbal",
                    "Creating directory",
                    "Build Engine version",
                    "Copying file from ");
        }
        finally
        {
            await RunDotnet("build-server shutdown");
        }
    }

    static Task<BufferedCommandResult> RunDotnet(string arguments)
    {
        var solutionDir = AttributeReader.GetSolutionDirectory();
        return Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();
    }

    public static IEnumerable<object?[]> GetData()
    {
        foreach (var environmentCache in new[] {true, false})
        foreach (var propertyCache in new[] {true, false})
        {
            yield return new object?[]
            {
                environmentCache,
                propertyCache
            };
        }
    }
}
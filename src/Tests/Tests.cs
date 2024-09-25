using CliWrap;
using CliWrap.Buffered;
using DiffEngine;

public class Tests : IAsyncDisposable
{
    static string solutionDir;
    static string cacheDirectory;

    static Tests()
    {
        solutionDir = AttributeReader.GetSolutionDirectory();
        cacheDirectory = Path.Combine(solutionDir, "Cache");
        if (Directory.Exists(cacheDirectory))
        {
            Directory.Delete(cacheDirectory, true);
        }
    }

    [Fact]
    public void HasEmbedded()
    {
        Assert.True(SymbolChecker.HasEmbedded(typeof(Tests).Assembly.Location));
        Assert.False(SymbolChecker.HasEmbedded(typeof(object).Assembly.Location));
    }

    [Theory]
    [MemberData(nameof(GetData))]
    public async Task RunTask(bool environmentCache, bool propertyCache)
    {
        var sampleAppPath = Path.Combine(solutionDir, "SampleApp");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await RunDotnet("build --configuration IncludeTask");

        var environmentVariables = new Dictionary<string, string?>();
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
            .WithValidation(CommandResultValidation.None)
            .WithEnvironmentVariables(environmentVariables)
            .ExecuteBufferedAsync();

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
            .UniqueForRuntime()
            .ScrubEmptyLines()
            .ScrubLinesWithReplace(_ => _.Replace('\\', '/'))
            .ScrubLinesContaining(
                "Build started",
                "Time Elapsed",
                "Finished Cymbal",
                "Creating directory",
                "Build Engine version",
                "Copying file from ",
                "Copyright (C) Microsoft Corporation",
                "Workload updates are available",
                "MSBuild version");
    }

    [Fact]
    public async Task Should_Parse_SymbolServer()
    {
        var sampleAppPath = Path.Combine(solutionDir, "SampleWithSymbolServer");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await RunDotnet("build --configuration IncludeTask");

        var arguments = "publish --configuration IncludeTask --no-build --no-restore --verbosity normal";

        var publishResult = await Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(sampleAppPath)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (publishResult.StandardError.Length > 0)
        {
            throw new(publishResult.StandardError);
        }

        if (publishResult.StandardOutput.Contains("error"))
        {
            throw new(publishResult.StandardOutput.Replace(solutionDir, ""));
        }

        var appPath = Path.Combine(solutionDir, "SampleWithSymbolServer/bin/IncludeTask/SampleWithSymbolServer.dll");
        var runResult = await RunDotnet(appPath);

        await Verify(
                new
                {
                    buildOutput = publishResult.StandardOutput,
                    consoleOutput = runResult.StandardOutput,
                    consoleError = runResult.StandardError
                })
            .ScrubLinesWithReplace(_ => _.Replace('\\', '/'))
            .ScrubLinesContaining(
                "Build started",
                "Time Elapsed",
                "Finished Cymbal",
                "Creating directory",
                "Build Engine version",
                "Copying file from ",
                "Copyright (C) Microsoft Corporation",
                "MSBuild version");
    }

    static Task<BufferedCommandResult> RunDotnet(string arguments) =>
        Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

    static bool[] bools =
    [
        true,
        false
    ];

    public static IEnumerable<object?[]> GetData()
    {
        foreach (var environmentCache in bools)
        foreach (var propertyCache in bools)
        {
            yield return
            [
                environmentCache,
                propertyCache
            ];
        }
    }

    public ValueTask DisposeAsync()
    {
        if (BuildServerDetector.Detected)
        {
            return ValueTask.CompletedTask;
        }

        return new (RunDotnet("build-server shutdown"));
    }
}
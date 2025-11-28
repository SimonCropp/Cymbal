using CliWrap;
using CliWrap.Buffered;
using DiffEngine;

public class Tests : IAsyncDisposable
{
    static string solutionDir;
    static string cacheDirectory;

    static Tests()
    {
        solutionDir = ProjectFiles.SolutionDirectory;
        cacheDirectory = Path.Combine(solutionDir, "Cache");
        if (Directory.Exists(cacheDirectory))
        {
            Directory.Delete(cacheDirectory, true);
        }
    }

    [Test]
    public void HasEmbedded()
    {
        True(SymbolChecker.HasEmbedded(typeof(Tests).Assembly.Location));
        False(SymbolChecker.HasEmbedded(typeof(object).Assembly.Location));
    }

    [Test]
    public async Task RunTask([Values] bool environmentCache, [Values] bool propertyCache)
    {
        var sampleAppPath = Path.Combine(solutionDir, "SampleApp");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await RunDotnet("build --configuration IncludeTask --no-incremental");

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

        var publishError = publishResult.StandardError;
        if (publishError.Length > 0)
        {
            throw new(publishError);
        }

        var publishOutput = publishResult.StandardOutput;
        if (publishOutput.Contains("error"))
        {
            throw new(publishOutput.Replace(solutionDir, ""));
        }

        var appPath = Path.Combine(solutionDir, "SampleApp/bin/IncludeTask/SampleApp.dll");
        var runResult = await RunDotnet(appPath);

        await Verify(
                new
                {
                    buildOutput = publishOutput,
                    consoleOutput = runResult.StandardOutput,
                    consoleError = runResult.StandardError
                })
            .UseParameters(environmentCache, propertyCache)
            .UniqueForRuntime();
    }

    [Test]
    public async Task Should_Parse_SymbolServer()
    {
        var sampleAppPath = Path.Combine(solutionDir, "SampleWithSymbolServer");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await RunDotnet("build --configuration IncludeTask --no-incremental");

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
            });
    }

    static Task<BufferedCommandResult> RunDotnet(string arguments) =>
        Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

    public ValueTask DisposeAsync()
    {
        if (BuildServerDetector.Detected)
        {
            return ValueTask.CompletedTask;
        }

        return new(RunDotnet("build-server shutdown"));
    }
}
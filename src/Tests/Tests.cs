using CliWrap;
using CliWrap.Buffered;

[UsesVerify]
public class Tests
{
    [Fact]
    public async Task RunTask()
    {
        var solutionDir = AttributeReader.GetSolutionDirectory();
        var sampleAppPath = Path.Combine(solutionDir, "SampleApp");
        var includeTaskDir = Path.Combine(sampleAppPath, @"bin\IncludeTask");
        if (Directory.Exists(includeTaskDir))
        {
            Directory.Delete(includeTaskDir, true);
        }

        await Cli.Wrap("dotnet")
            .WithArguments("build --force --configuration IncludeTask --no-incremental")
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        var publishResult = await Cli.Wrap("dotnet")
            .WithArguments("publish --configuration IncludeTask --force --no-build --no-restore --verbosity normal")
            .WithWorkingDirectory(sampleAppPath)
            .WithValidation(CommandResultValidation.None)
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
            var runResult = await Cli.Wrap("dotnet")
                .WithArguments(appPath)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            await Verify(
                    new
                    {
                        buildOutput = publishResult.StandardOutput,
                        consoleOutput = runResult.StandardOutput,
                        consoleError = runResult.StandardError
                    })
                .ScrubLinesWithReplace(line => line.Replace('\\', '/'))
                .ScrubLinesContaining("Build started")
                .ScrubLinesContaining("Time Elapsed")
                .ScrubLinesContaining("Finished Cymbal")
                .ScrubLinesContaining("Build Engine version")
                .ScrubLinesContaining("Copying file from ");
        }
        finally
        {
            await Cli.Wrap("dotnet")
                .WithArguments("build-server shutdown")
                .ExecuteAsync();
            ;
        }
    }
}
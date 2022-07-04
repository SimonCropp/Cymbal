using CliWrap;
using CliWrap.Buffered;

[UsesVerify]
public class Tests
{
    [Fact]
    public async Task RunTask()
    {
        var solutionDir = AttributeReader.GetSolutionDirectory();

        var buildResult = await Cli.Wrap("dotnet")
            .WithArguments("publish --configuration IncludeTask")
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        var shutdown = Cli.Wrap("dotnet")
            .WithArguments("build-server shutdown")
            .ExecuteAsync();

        try
        {
            if (buildResult.StandardError.Length > 0)
            {
                throw new(buildResult.StandardError);
            }

            if (buildResult.StandardOutput.Contains("error"))
            {
                throw new(buildResult.StandardOutput.Replace(solutionDir, ""));
            }

            var appPath = Path.Combine(solutionDir, "SampleApp/bin/IncludeTask/SampleApp.dll");
            var runResult = await Cli.Wrap("dotnet")
                .WithArguments(appPath)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            await Verify(
                    new
                    {
                        buildOutput = buildResult.StandardOutput,
                        consoleOutput = runResult.StandardOutput,
                        consoleError = runResult.StandardError
                    })
                .ScrubLinesContaining(
                    " -> ",
                    "You are using a preview version",
                    "Build Engine version",
                    "Time Elapsed")
                .ScrubLinesWithReplace(line => line.Replace('\\', '/'));
        }
        finally
        {
            await shutdown;
        }
    }
}
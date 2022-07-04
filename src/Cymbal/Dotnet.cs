public static class ProcessRunner
{
    public static string Execute(string command,string arguments)
    {
        using var process = new Process
        {
            StartInfo = new()
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var errorBuilder = new StringBuilder();
        var outputBuilder = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            outputBuilder.AppendLine(args.Data);
        };
        process.BeginOutputReadLine();
        process.ErrorDataReceived += (_, args) =>
        {
            errorBuilder.AppendLine(args.Data);
        };
        process.BeginErrorReadLine();
        if (!process.DoubleWaitForExit())
        {
            var timeoutError = $@"Process timed out. Command line: {command} {arguments}.
Output: {outputBuilder}
Error: {errorBuilder}";
            throw new ErrorException(timeoutError);
        }

        if (process.ExitCode == 0)
        {
            return outputBuilder.ToString();
        }

        var error = $@"Could not execute process. Command line: {command} {arguments}.
Output: {outputBuilder}
Error: {errorBuilder}";
        throw new ErrorException(error);
    }
}
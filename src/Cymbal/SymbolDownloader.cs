public static class SymbolDownloader
{
    public static (List<string> missingSymbols, List<string> foundSymbols) Run(string? cacheDirectory, List<string> toDownload, string[] symbolServers)
    {
        var arguments = BuildArguments(cacheDirectory, toDownload, symbolServers);

        var result = ProcessRunner.Execute("dotnet", arguments);

        var missingSymbols = new List<string>();
        var foundSymbols = new List<string>();
        foreach (var line in result)
        {
            ProcessLine(line, missingSymbols, foundSymbols);
        }

        foreach (var foundFileName in foundSymbols.Select(Path.GetFileName))
        {
            missingSymbols.Remove(foundFileName);
        }

        return (missingSymbols, foundSymbols);
    }

    static void ProcessLine(string line, List<string> missingSymbols, List<string> foundSymbols)
    {
        if (line.StartsWith("ERROR: Not Found: "))
        {
            var scrubbedLine = line.Replace("ERROR: Not Found: ", "");
            var indexOfDash = scrubbedLine.IndexOf(" - ");
            var missing = scrubbedLine.Substring(0, indexOfDash);
            if (!missingSymbols.Contains(missing))
            {
                missingSymbols.Add(missing);
            }
        }
        else if (line.StartsWith("Writing: "))
        {
            var scrubbedLine = line.Replace("Writing: ", "");
            foundSymbols.Add(scrubbedLine);
        }
    }

    static string BuildArguments(string? cacheDirectory, List<string> toDownload, string[] symbolServers)
    {
        var arguments = "tool run dotnet-symbol ";

        foreach (var server in symbolServers)
        {
            arguments += $"--server-path {server} ";
        }

        if (cacheDirectory != null)
        {
            arguments += $"--cache-directory {cacheDirectory} ";
        }

        arguments += string.Join(" ", toDownload);
        return arguments;
    }
}
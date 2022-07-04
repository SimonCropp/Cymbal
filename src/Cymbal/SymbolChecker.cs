using System.Reflection.PortableExecutable;

public static class SymbolChecker
{
    public static bool HasEmbedded(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new PEReader(stream);
        var entries = reader.ReadDebugDirectory();
        return ImmutableArrayExtensions.Any(entries, e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
    }

    static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
}
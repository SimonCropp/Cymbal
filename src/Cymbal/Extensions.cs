static class Extensions
{
    //To work around https://github.com/dotnet/runtime/issues/27128
    public static bool DoubleWaitForExit(this Process process)
    {
        var result = process.WaitForExit(60000);
        if (result)
        {
            process.WaitForExit();
        }

        return result;
    }
}
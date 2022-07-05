static class Extensions
{
    //To work around https://github.com/dotnet/runtime/issues/27128
    public static bool DoubleWaitForExit(this Process process)
    {
        //4min30sec
        var timeout = 270000;
        var result = process.WaitForExit(timeout);
        if (result)
        {
            process.WaitForExit();
        }

        return result;
    }
}
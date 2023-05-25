[assembly: CollectionBehavior(DisableTestParallelization = true)]

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() =>
        VerifyDiffPlex.Initialize();
}
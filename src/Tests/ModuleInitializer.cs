[assembly: NonParallelizable]

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.InitializePlugins();

        VerifierSettings.ScrubEmptyLines();
        VerifierSettings.ScrubLinesWithReplace(_ => _.Replace('\\', '/'));
        VerifierSettings.ScrubLinesContaining(
            "CopyResolved",
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
}
namespace AudioLab.Audio.Tests.Support;

/// <summary>Tells whether code settles into allocating nothing, once the runtime has finished compiling it.</summary>
public static class AllocationProbe
{
    private const int WindowBlocks = 20;
    private const int WindowsToTry = 60;
    private const int CleanWindowsNeeded = 6;

    /// <summary>Runs the action in windows until six in a row allocate nothing; a call that allocates every time never gets there.</summary>
    public static bool SettlesAtZero(Action block)
    {
        int clean = 0;
        for (int window = 0; window < WindowsToTry && clean < CleanWindowsNeeded; window++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < WindowBlocks; i++)
            {
                block();
            }
            clean = GC.GetAllocatedBytesForCurrentThread() == before ? clean + 1 : 0;
        }
        return clean >= CleanWindowsNeeded;
    }
}

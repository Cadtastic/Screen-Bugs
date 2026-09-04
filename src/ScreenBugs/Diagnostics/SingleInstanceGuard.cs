namespace ScreenBugs.Diagnostics;

/// <summary>Named mutex so only one overlay runs per user session.</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\ScreenBugs.SingleInstance";

    private readonly Mutex mutex = new(initiallyOwned: false, MutexName);
    private bool acquired;

    /// <summary>True if this process now owns the instance slot; false if another instance holds it.</summary>
    public bool TryAcquire()
    {
        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing; the slot is ours.
            acquired = true;
        }

        return acquired;
    }

    public void Dispose()
    {
        if (acquired)
        {
            mutex.ReleaseMutex();
            acquired = false;
        }

        mutex.Dispose();
    }
}

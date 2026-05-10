using PicoGK;

namespace PicoMoldForge.PicoGK.Runtime;

public static class PicoGkTaskRunner
{
    private const string MutexName = "PicoMoldForge.PicoGK.LibraryGo";

    public static T Run<T>(Func<T> task, float voxelSizeMm = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var mutex = new Mutex(
            initiallyOwned: false,
            name: MutexName);

        var lockTaken = false;

        try
        {
            lockTaken = mutex.WaitOne(TimeSpan.FromMinutes(2));

            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for exclusive PicoGK runtime access.");
            }

            T? result = default;
            Exception? capturedException = null;

            var logDirectory = Path.Combine(Path.GetTempPath(), "PicoMoldForge", "PicoGKLogs");
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(
                logDirectory,
                $"PicoGK-{Environment.ProcessId}-{Guid.NewGuid():N}.log");

            Library.Go(
                voxelSizeMm,
                () =>
                {
                    try
                    {
                        result = task();
                    }
                    catch (Exception ex)
                    {
                        capturedException = ex;
                    }
                },
                logPath,
                true,
                "PicoMoldForge",
                string.Empty);

            if (capturedException is not null)
            {
                throw capturedException;
            }

            if (result is null)
            {
                throw new InvalidOperationException("PicoGK task completed without returning a result.");
            }

            return result;
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
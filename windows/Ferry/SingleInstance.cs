namespace Ferry;

/// <summary>
/// Keeps Ferry to one copy per user session. Starting it again signals the
/// running copy instead, which brings up the shelf.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string Name = @"Local\io.github.p-poweska.Ferry";
    private const string ShowName = Name + ".ShowShelf";

    private readonly Mutex mutex;
    private readonly EventWaitHandle showRequest = new(false, EventResetMode.AutoReset, ShowName);
    private readonly RegisteredWaitHandle registration;

    private SingleInstance(Mutex mutex)
    {
        this.mutex = mutex;
        registration = ThreadPool.RegisterWaitForSingleObject(
            showRequest, (_, _) => ShowRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Raised on a background thread when another launch asks for the shelf.</summary>
    public event Action? ShowRequested;

    /// <summary>The claim on being the running copy, or <c>null</c> when Ferry already runs.</summary>
    public static SingleInstance? TryClaim()
    {
        var mutex = new Mutex(initiallyOwned: true, Name, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }
        mutex.Dispose();
        return null;
    }

    public static void AskFirstToShowShelf()
    {
        if (EventWaitHandle.TryOpenExisting(ShowName, out var showRequest))
        {
            using (showRequest)
            {
                showRequest.Set();
            }
        }
    }

    public void Dispose()
    {
        registration.Unregister(null);
        showRequest.Dispose();
        mutex.Dispose();
    }
}

using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

public static class Program
{
    /// <summary>This copy's claim on being the one that runs.</summary>
    internal static SingleInstance Instance { get; private set; } = null!;

    [STAThread]
    private static void Main()
    {
        using var instance = SingleInstance.TryClaim();
        if (instance is null)
        {
            SingleInstance.AskFirstToShowShelf();
            return;
        }
        Instance = instance;

        // Drag and drop needs OLE, not just COM. ANSI file names from older
        // apps need the system code page.
        OleInitialize(0);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callback =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }
}

using Microsoft.Win32;

namespace Ferry;

/// <summary>Starts Ferry when the user signs in, through the usual Run key.</summary>
internal static class LaunchAtLogin
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    /// <summary>Where Task Manager and Settings record startup apps the user turned off.</summary>
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string Name = "Ferry";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// On only when the entry starts this copy of Ferry and wasn't turned off
    /// in Task Manager, so moving the app folder shows it as off.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(Name) is not string command || !command.Equals(Command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            // An odd first byte means disabled.
            return approved?.GetValue(Name) is not byte[] { Length: > 0 } state || (state[0] & 1) == 0;
        }
    }

    public static void Set(bool isOn)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        if (isOn)
        {
            run.SetValue(Name, Command);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            approved?.DeleteValue(Name, throwOnMissingValue: false);
        }
        else
        {
            run.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}

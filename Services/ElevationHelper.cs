using System.Diagnostics;
using System.Security.Principal;

namespace ProxySwitcher.Services;

/// <summary>
/// Handles elevation of privileges for operations that require administrator rights.
/// </summary>
public class ElevationHelper
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsElevated()
    {
        try
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Requests elevation for the current application if not already running as admin.
    /// Returns true if elevation was granted, false if cancelled.
    /// </summary>
    public static bool RequestElevation()
    {
        if (IsElevated())
            return true;

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                           ?? System.AppContext.BaseDirectory + "ProxySwitcher.exe",
                UseShellExecute = true,
                Verb = "runas" // This triggers UAC
            };

            using (var process = Process.Start(processInfo))
            {
                process?.WaitForExit();
                return true; // If we got here, elevation was granted
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt or elevation failed
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Elevation request failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executes an action with elevation if needed. If not elevated, requests elevation.
    /// Returns true if action was executed or elevation was successful.
    /// </summary>
    public static bool ExecuteWithElevationIfNeeded(Action action, string operationName = "operation")
    {
        if (IsElevated())
        {
            try
            {
                action();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Even with elevation, access denied - request elevation restart
                return RequestElevation();
            }
        }
        else
        {
            // Request elevation
            return RequestElevation();
        }
    }
}

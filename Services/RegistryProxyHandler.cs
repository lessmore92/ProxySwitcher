using Microsoft.Win32;

namespace ProxySwitcher.Services;

/// <summary>
/// Manages Windows system proxy settings via the Windows Registry.
/// </summary>
public class RegistryProxyHandler
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string ProxyEnableKeyName = "ProxyEnable";
    private const string ProxyServerKeyName = "ProxyServer";

    /// <summary>
    /// Applies proxy settings to the Windows Registry.
    /// Requires elevated (administrator) privileges.
    /// </summary>
    public void ApplySystemProxy(string host, int port)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
            {
                if (key == null)
                    throw new InvalidOperationException($"Could not open registry key: {RegistryKeyPath}");

                // Set ProxyEnable to 1 (enabled)
                key.SetValue(ProxyEnableKeyName, 1, RegistryValueKind.DWord);

                // Set ProxyServer to "host:port"
                var proxyServer = $"{host}:{port}";
                key.SetValue(ProxyServerKeyName, proxyServer, RegistryValueKind.String);
            }

            // Refresh system proxy settings for all open connections
            NotifySystemOfProxyChange();
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Administrator privileges required to modify system proxy settings. Please run the application as administrator or the elevation will be requested.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply system proxy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clears/disables proxy settings in the Windows Registry.
    /// Requires elevated (administrator) privileges.
    /// </summary>
    public void ClearSystemProxy()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
            {
                if (key == null)
                    throw new InvalidOperationException($"Could not open registry key: {RegistryKeyPath}");

                // Set ProxyEnable to 0 (disabled)
                key.SetValue(ProxyEnableKeyName, 0, RegistryValueKind.DWord);

                // Clear ProxyServer
                if (key.GetValue(ProxyServerKeyName) != null)
                {
                    key.DeleteValue(ProxyServerKeyName);
                }
            }

            // Refresh system proxy settings for all open connections
            NotifySystemOfProxyChange();
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Administrator privileges required to modify system proxy settings.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to clear system proxy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads current proxy settings from the Windows Registry.
    /// </summary>
    public (string? Host, int Port, bool IsEnabled) GetCurrentSystemProxy()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false))
            {
                if (key == null)
                    return (null, 0, false);

                var proxyEnable = (int?)key.GetValue(ProxyEnableKeyName) ?? 0;
                var proxyServer = (string?)key.GetValue(ProxyServerKeyName);

                if (proxyEnable == 0 || string.IsNullOrEmpty(proxyServer))
                    return (null, 0, false);

                // Parse "host:port" format
                var parts = proxyServer.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                {
                    return (parts[0], port, true);
                }

                return (proxyServer, 0, true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading system proxy: {ex.Message}");
            return (null, 0, false);
        }
    }

    /// <summary>
    /// Notifies Windows that proxy settings have changed.
    /// This broadcasts the WM_SETTINGCHANGE message to all windows.
    /// </summary>
    private static void NotifySystemOfProxyChange()
    {
        try
        {
            // Use WinInet.InternetSetOption to notify of proxy change
            // This is the standard Windows way to refresh proxy settings
            NativeInterop.InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0); // INTERNET_OPTION_PROXY_SETTINGS_CHANGED = 39
        }
        catch
        {
            // If notification fails, it's not critical - proxy settings are still changed
        }
    }
}

/// <summary>
/// Native Windows interop for internet settings.
/// </summary>
internal static class NativeInterop
{
    [System.Runtime.InteropServices.DllImport("wininet.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}

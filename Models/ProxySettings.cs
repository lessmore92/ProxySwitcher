namespace ProxySwitcher.Models;

/// <summary>
/// Represents the current proxy settings state of the system.
/// </summary>
public class ProxySettings
{
    /// <summary>
    /// Current proxy host from system settings (null if not set).
    /// </summary>
    public string? ProxyHost { get; set; }

    /// <summary>
    /// Current proxy port from system settings (0 if not set).
    /// </summary>
    public int ProxyPort { get; set; }

    /// <summary>
    /// Whether proxy is currently enabled in system settings.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Currently active profile name (null if no profile is active).
    /// </summary>
    public string? ActiveProfileName { get; set; }

    public override string ToString()
    {
        if (!IsEnabled || string.IsNullOrEmpty(ProxyHost))
            return "Proxy: Disabled";

        var profileInfo = !string.IsNullOrEmpty(ActiveProfileName) 
            ? $" (Profile: {ActiveProfileName})" 
            : "";
        
        return $"Proxy: {ProxyHost}:{ProxyPort}{profileInfo}";
    }
}

namespace ProxySwitcher.Models;

/// <summary>
/// Represents a single proxy configuration profile.
/// </summary>
public class ProxyProfile
{
    /// <summary>
    /// Unique name for this profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Proxy server hostname or IP address.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Proxy server port number (1-65535).
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Whether to apply this proxy to Windows system settings.
    /// </summary>
    public bool EnableSystemProxy { get; set; } = true;

    /// <summary>
    /// Whether to set environment variables (HTTP_PROXY, HTTPS_PROXY, ALL_PROXY).
    /// </summary>
    public bool EnableEnvironmentVariables { get; set; } = false;

    /// <summary>
    /// Validates the profile for required fields and valid ranges.
    /// </summary>
    /// <returns>List of validation error messages. Empty if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Profile name is required.");

        if (string.IsNullOrWhiteSpace(Host))
            errors.Add("Proxy host is required.");

        if (Port < 1 || Port > 65535)
            errors.Add("Proxy port must be between 1 and 65535.");

        return errors;
    }

    /// <summary>
    /// Returns a display-friendly string representation.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Host}:{Port})";
    }
}

using ProxySwitcher.Data;
using ProxySwitcher.Models;

namespace ProxySwitcher.Services;

/// <summary>
/// Orchestrates proxy activation and deactivation.
/// Coordinates between ProfileManager, RegistryProxyHandler, and EnvironmentProxyHandler.
/// </summary>
public class ProxySwitcher
{
    private readonly ProfileManager _profileManager;
    private readonly RegistryProxyHandler _registryHandler;
    private readonly EnvironmentProxyHandler _environmentHandler;

    public event EventHandler? StatusChanged;

    public ProxySwitcher(
        ProfileManager profileManager,
        RegistryProxyHandler registryHandler,
        EnvironmentProxyHandler environmentHandler)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _registryHandler = registryHandler ?? throw new ArgumentNullException(nameof(registryHandler));
        _environmentHandler = environmentHandler ?? throw new ArgumentNullException(nameof(environmentHandler));

        // Subscribe to profile changes
        _profileManager.ActiveProfileChanged += (s, e) => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Activates a proxy profile by applying its settings to the system.
    /// </summary>
    public void ActivateProfile(string profileName)
    {
        var profile = _profileManager.GetProfile(profileName);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{profileName}' not found.");

        try
        {
            // First deactivate any currently active proxy
            var previousProfile = _profileManager.GetActiveProfile();
            if (previousProfile != null)
            {
                DeactivateProxy();
            }

            // Apply system proxy if enabled in profile
            if (profile.EnableSystemProxy)
            {
                _registryHandler.ApplySystemProxy(profile.Host, profile.Port);
            }

            // Set environment variables if enabled in profile
            if (profile.EnableEnvironmentVariables)
            {
                _environmentHandler.SetEnvironmentVariables(profile.Host, profile.Port);
            }

            // Mark profile as active in ProfileManager
            _profileManager.SetActiveProfile(profileName);

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Failed to activate proxy: Administrator privileges required. {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to activate proxy profile '{profileName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deactivates the currently active proxy.
    /// </summary>
    public void DeactivateProxy()
    {
        try
        {
            var activeProfile = _profileManager.GetActiveProfile();
            if (activeProfile == null)
                return; // No active profile to deactivate

            // Disable system proxy if it was enabled
            if (activeProfile.EnableSystemProxy)
            {
                _registryHandler.ClearSystemProxy();
            }

            // Clear environment variables if they were enabled
            if (activeProfile.EnableEnvironmentVariables)
            {
                _environmentHandler.ClearEnvironmentVariables();
            }

            // Clear active profile
            _profileManager.ClearActiveProfile();

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Failed to deactivate proxy: Administrator privileges required. {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deactivate proxy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current proxy status.
    /// </summary>
    public ProxySettings GetStatus()
    {
        var (host, port, isEnabled) = _registryHandler.GetCurrentSystemProxy();
        var activeProfileName = _profileManager.GetActiveProfileName();

        return new ProxySettings
        {
            ProxyHost = host,
            ProxyPort = port,
            IsEnabled = isEnabled,
            ActiveProfileName = activeProfileName
        };
    }
}

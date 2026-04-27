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
    public event EventHandler<string>? ProgressChanged;
    public event EventHandler<bool>? IsBusyChanged;

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
    public async Task ActivateProfileAsync(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));

        ProgressChanged?.Invoke(this, "Preparing profile activation...");
        IsBusyChanged?.Invoke(this, true);

        try
        {
            await Task.Run(() => ActivateProfileInternal(profileName));
            ProgressChanged?.Invoke(this, "Profile activation completed.");
        }
        catch (Exception ex)
        {
            ProgressChanged?.Invoke(this, $"Activation failed: {ex.Message}");
            throw;
        }
        finally
        {
            IsBusyChanged?.Invoke(this, false);
        }
    }

    private void ActivateProfileInternal(string profileName)
    {
        var profile = _profileManager.GetProfile(profileName);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{profileName}' not found.");

        try
        {
            ProgressChanged?.Invoke(this, "Checking active proxy profile...");
            var previousProfile = _profileManager.GetActiveProfile();
            if (previousProfile != null)
            {
                ProgressChanged?.Invoke(this, "Clearing currently active proxy...");
                DeactivateProxyInternal(previousProfile);
            }

            if (profile.EnableSystemProxy)
            {
                ProgressChanged?.Invoke(this, "Applying system proxy settings...");
                _registryHandler.ApplySystemProxy(profile.Host, profile.Port);
            }

            if (profile.EnableEnvironmentVariables)
            {
                ProgressChanged?.Invoke(this, "Setting environment variables...");
                _environmentHandler.SetEnvironmentVariables(profile.Host, profile.Port);
            }

            ProgressChanged?.Invoke(this, "Saving active profile...");
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
    public async Task DeactivateProxyAsync()
    {
        ProgressChanged?.Invoke(this, "Preparing to disable proxy...");
        IsBusyChanged?.Invoke(this, true);

        try
        {
            await Task.Run(() =>
            {
                var activeProfile = _profileManager.GetActiveProfile();
                if (activeProfile == null)
                {
                    ProgressChanged?.Invoke(this, "No active proxy to disable.");
                    return;
                }

                DeactivateProxyInternal(activeProfile);
            });

            ProgressChanged?.Invoke(this, "Proxy disabled.");
        }
        catch (Exception ex)
        {
            ProgressChanged?.Invoke(this, $"Disable failed: {ex.Message}");
            throw;
        }
        finally
        {
            IsBusyChanged?.Invoke(this, false);
        }
    }

    private void DeactivateProxyInternal(ProxyProfile activeProfile)
    {
        try
        {
            ProgressChanged?.Invoke(this, "Clearing system proxy settings...");
            if (activeProfile.EnableSystemProxy)
            {
                _registryHandler.ClearSystemProxy();
            }

            ProgressChanged?.Invoke(this, "Clearing environment variables...");
            if (activeProfile.EnableEnvironmentVariables)
            {
                _environmentHandler.ClearEnvironmentVariables();
            }

            ProgressChanged?.Invoke(this, "Saving deactivated state...");
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

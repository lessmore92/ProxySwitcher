using ProxySwitcher.Data;
using ProxySwitcher.Models;

namespace ProxySwitcher.Services;

/// <summary>
/// Manages proxy profiles and their persistence.
/// </summary>
public class ProfileManager
{
    private readonly ProfileRepository _repository;
    private List<ProxyProfile> _profiles;
    private string? _activeProfileName;

    public event EventHandler? ProfilesChanged;
    public event EventHandler? ActiveProfileChanged;

    public ProfileManager(ProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _profiles = _repository.LoadProfiles();
        _activeProfileName = _repository.LoadActiveProfile();
    }

    /// <summary>
    /// Gets all profiles.
    /// </summary>
    public List<ProxyProfile> GetAllProfiles()
    {
        return new List<ProxyProfile>(_profiles); // Return a copy to prevent external modification
    }

    /// <summary>
    /// Gets a specific profile by name.
    /// </summary>
    public ProxyProfile? GetProfile(string name)
    {
        return _profiles.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Gets the currently active profile name.
    /// </summary>
    public string? GetActiveProfileName()
    {
        return _activeProfileName;
    }

    /// <summary>
    /// Gets the currently active profile object.
    /// </summary>
    public ProxyProfile? GetActiveProfile()
    {
        return _activeProfileName != null ? GetProfile(_activeProfileName) : null;
    }

    /// <summary>
    /// Creates a new profile and persists it.
    /// </summary>
    public void CreateProfile(string name, string host, int port, bool enableSystemProxy, bool enableEnvironmentVariables)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));

        if (_profiles.Any(p => p.Name == name))
            throw new InvalidOperationException($"Profile '{name}' already exists.");

        var profile = new ProxyProfile
        {
            Name = name,
            Host = host,
            Port = port,
            EnableSystemProxy = enableSystemProxy,
            EnableEnvironmentVariables = enableEnvironmentVariables
        };

        var errors = profile.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Profile validation failed: {string.Join(", ", errors)}", nameof(profile));

        _profiles.Add(profile);
        _repository.SaveProfiles(_profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates an existing profile and persists it.
    /// </summary>
    public void UpdateProfile(string originalName, string newName, string host, int port, bool enableSystemProxy, bool enableEnvironmentVariables)
    {
        var profile = GetProfile(originalName);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{originalName}' not found.");

        // Check if new name already exists (if different from original name)
        if (newName != originalName && _profiles.Any(p => p.Name == newName))
            throw new InvalidOperationException($"Profile '{newName}' already exists.");

        profile.Name = newName;
        profile.Host = host;
        profile.Port = port;
        profile.EnableSystemProxy = enableSystemProxy;
        profile.EnableEnvironmentVariables = enableEnvironmentVariables;

        var errors = profile.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Profile validation failed: {string.Join(", ", errors)}", nameof(profile));

        // If the renamed profile was active, update active profile name
        if (_activeProfileName == originalName)
        {
            _activeProfileName = newName;
            _repository.SaveActiveProfile(_activeProfileName);
        }

        _repository.SaveProfiles(_profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a profile and persists the change.
    /// </summary>
    public void DeleteProfile(string name)
    {
        var profile = _profiles.FirstOrDefault(p => p.Name == name);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{name}' not found.");

        _profiles.Remove(profile);

        // If deleted profile was active, clear active profile
        if (_activeProfileName == name)
        {
            _activeProfileName = null;
            _repository.SaveActiveProfile(null);
            ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
        }

        _repository.SaveProfiles(_profiles);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets a profile as the active profile.
    /// </summary>
    public void SetActiveProfile(string profileName)
    {
        var profile = GetProfile(profileName);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{profileName}' not found.");

        _activeProfileName = profileName;
        _repository.SaveActiveProfile(profileName);
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the active profile selection.
    /// </summary>
    public void ClearActiveProfile()
    {
        _activeProfileName = null;
        _repository.SaveActiveProfile(null);
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks if a profile name is valid and available.
    /// </summary>
    public bool IsValidProfileName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && !_profiles.Any(p => p.Name == name);
    }
}

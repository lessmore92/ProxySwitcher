using System.IO;
using System.Text.Json;
using ProxySwitcher.Models;

namespace ProxySwitcher.Data;

/// <summary>
/// Handles persistence of proxy profiles to JSON storage.
/// </summary>
public class ProfileRepository
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProxySwitcher");

    private static readonly string ProfilesFilePath = Path.Combine(AppDataPath, "profiles.json");
    private static readonly string ActiveProfileFilePath = Path.Combine(AppDataPath, "active_profile.txt");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProfileRepository()
    {
        // Ensure AppData directory exists
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    /// <summary>
    /// Loads all profiles from storage.
    /// </summary>
    public List<ProxyProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesFilePath))
            {
                return new List<ProxyProfile>();
            }

            var json = File.ReadAllText(ProfilesFilePath);
            var profiles = JsonSerializer.Deserialize<List<ProxyProfile>>(json, _jsonOptions);
            return profiles ?? new List<ProxyProfile>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading profiles: {ex.Message}");
            return new List<ProxyProfile>();
        }
    }

    /// <summary>
    /// Saves all profiles to storage.
    /// </summary>
    public void SaveProfiles(List<ProxyProfile> profiles)
    {
        try
        {
            var json = JsonSerializer.Serialize(profiles, _jsonOptions);
            File.WriteAllText(ProfilesFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving profiles: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads the name of the currently active profile.
    /// </summary>
    public string? LoadActiveProfile()
    {
        try
        {
            if (!File.Exists(ActiveProfileFilePath))
            {
                return null;
            }

            var profileName = File.ReadAllText(ActiveProfileFilePath).Trim();
            return string.IsNullOrEmpty(profileName) ? null : profileName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading active profile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves the name of the currently active profile.
    /// </summary>
    public void SaveActiveProfile(string? profileName)
    {
        try
        {
            File.WriteAllText(ActiveProfileFilePath, profileName ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving active profile: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the path where profiles are stored (for information purposes).
    /// </summary>
    public string GetStoragePath() => AppDataPath;
}

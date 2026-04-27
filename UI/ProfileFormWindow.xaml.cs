using System.Windows;
using ProxySwitcher.Models;
using ProxySwitcher.Services;

namespace ProxySwitcher.UI;

/// <summary>
/// Dialog for creating or editing a proxy profile.
/// </summary>
public partial class ProfileFormWindow : Window
{
    private readonly ProfileManager _profileManager;
    private readonly ProxyProfile? _existingProfile;

    public ProfileFormWindow(ProfileManager profileManager, ProxyProfile? profileToEdit = null)
    {
        InitializeComponent();

        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _existingProfile = profileToEdit;

        if (_existingProfile != null)
        {
            Title = "Edit Profile";
            NameTextBox.Text = _existingProfile.Name;
            HostTextBox.Text = _existingProfile.Host;
            PortTextBox.Text = _existingProfile.Port.ToString();
            EnableSystemProxyCheckBox.IsChecked = _existingProfile.EnableSystemProxy;
            EnableEnvironmentVariablesCheckBox.IsChecked = _existingProfile.EnableEnvironmentVariables;
            NameTextBox.IsEnabled = false; // Prevent renaming via UI for now (handled by UpdateProfile)
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;
        ErrorMessage.Text = string.Empty;

        try
        {
            var name = NameTextBox.Text.Trim();
            var host = HostTextBox.Text.Trim();
            var portText = PortTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowError("Profile name is required.");
                return;
            }

            if (string.IsNullOrEmpty(host))
            {
                ShowError("Proxy host is required.");
                return;
            }

            if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
            {
                ShowError("Proxy port must be a number between 1 and 65535.");
                return;
            }

            var enableSystem = EnableSystemProxyCheckBox.IsChecked ?? true;
            var enableEnv = EnableEnvironmentVariablesCheckBox.IsChecked ?? false;

            if (_existingProfile == null)
            {
                // Create new profile
                _profileManager.CreateProfile(name, host, port, enableSystem, enableEnv);
            }
            else
            {
                // Update existing profile (name remains unchanged if edited this way)
                _profileManager.UpdateProfile(_existingProfile.Name, name, host, port, enableSystem, enableEnv);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Error saving profile: {ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }
}

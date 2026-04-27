using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ProxySwitcher.Models;
using ProxySwitcher.Services;
using ProxySwitcher.UI;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;

namespace ProxySwitcher;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// Receives shared services from <see cref="App"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ProfileManager _profileManager;
    private readonly ProxySwitcher.Services.ProxySwitcher _proxySwitcher;

    /// <summary>
    /// When true, the window will actually close instead of hiding to tray.
    /// Set by <see cref="App.ExitApplication"/>.
    /// </summary>
    public bool AllowClose { get; set; }

    public MainWindow(ProfileManager profileManager, ProxySwitcher.Services.ProxySwitcher proxySwitcher)
    {
        InitializeComponent();

        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _proxySwitcher = proxySwitcher ?? throw new ArgumentNullException(nameof(proxySwitcher));

        // Subscribe to events.
        _profileManager.ProfilesChanged += ProfileManager_ProfilesChanged;
        _profileManager.ActiveProfileChanged += ProfileManager_ActiveProfileChanged;
        _proxySwitcher.StatusChanged += ProxySwitcher_StatusChanged;
        _proxySwitcher.ProgressChanged += ProxySwitcher_ProgressChanged;
        _proxySwitcher.IsBusyChanged += ProxySwitcher_IsBusyChanged;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshProfileList();
        UpdateStatus();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose)
            return;

        // Hide to tray instead of closing.
        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
    }

    private void RefreshProfileList()
    {
        var profiles = _profileManager.GetAllProfiles();
        var activeName = _profileManager.GetActiveProfileName();

        ProfilesListBox.ItemsSource = null;
        ProfilesListBox.ItemsSource = profiles;

        if (profiles.Count == 0)
        {
            InfoTextBlock.Text = "No profiles defined. Click 'Create New' to add your first proxy profile.";
        }
        else
        {
            if (!string.IsNullOrEmpty(activeName))
            {
                InfoTextBlock.Text = $"Active profile: {activeName}\nDouble-click a profile to activate it.";

                // Try to select the active item for convenience.
                var activeProfile = profiles.FirstOrDefault(p => p.Name == activeName);
                if (activeProfile != null)
                    ProfilesListBox.SelectedItem = activeProfile;
            }
            else
            {
                InfoTextBlock.Text = "No profile is currently active. Select a profile and click 'Activate' or double-click it.";
            }
        }
    }

    private void UpdateStatus()
    {
        try
        {
            var status = _proxySwitcher.GetStatus();
            StatusTextBlock.Text = status.ToString();

            if (status.IsEnabled)
            {
                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // green
                StatusTextBlock.FontWeight = FontWeights.Bold;
            }
            else
            {
                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                StatusTextBlock.FontWeight = FontWeights.Normal;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error reading status: {ex.Message}";
        }
    }

    private void ProfileManager_ProfilesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshProfileList);
    }

    private void ProfileManager_ActiveProfileChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshProfileList();
            UpdateStatus();
        });
    }

    private void ProxySwitcher_StatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateStatus);
    }

    private void ProxySwitcher_ProgressChanged(object? sender, string? message)
    {
        if (message == null)
            return;

        Dispatcher.Invoke(() => SetOperationStatus(message));
    }

    private void ProxySwitcher_IsBusyChanged(object? sender, bool isBusy)
    {
        Dispatcher.Invoke(() => SetOperationBusy(isBusy));
    }

    private void SetOperationStatus(string message)
    {
        StepStatusTextBlock.Text = message;
    }

    private void SetOperationBusy(bool isBusy)
    {
        OperationProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        ActivateButton.IsEnabled = !isBusy;
        CreateButton.IsEnabled = !isBusy;
        EditButton.IsEnabled = !isBusy;
        DeleteButton.IsEnabled = !isBusy;
        DisableButton.IsEnabled = !isBusy;
        ProfilesListBox.IsEnabled = !isBusy;

        if (!isBusy && string.IsNullOrWhiteSpace(StepStatusTextBlock.Text))
        {
            StepStatusTextBlock.Text = "Ready";
        }
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var formWindow = new ProfileFormWindow(_profileManager) { Owner = this };
        if (formWindow.ShowDialog() == true)
        {
            RefreshProfileList();
            InfoTextBlock.Text = "Profile created successfully.";
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProxyProfile profile)
        {
            MessageBox.Show("Please select a profile to edit.", "No Profile Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var formWindow = new ProfileFormWindow(_profileManager, profile) { Owner = this };
        if (formWindow.ShowDialog() == true)
        {
            RefreshProfileList();
            InfoTextBlock.Text = "Profile updated successfully.";
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProxyProfile profile)
        {
            MessageBox.Show("Please select a profile to delete.", "No Profile Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete the profile '{profile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // If the profile being deleted is currently active, deactivate the proxy first.
                if (_profileManager.GetActiveProfileName() == profile.Name)
                {
                    try { await _proxySwitcher.DeactivateProxyAsync(); } catch { /* best-effort */ }
                }

                _profileManager.DeleteProfile(profile.Name);
                RefreshProfileList();
                InfoTextBlock.Text = "Profile deleted successfully.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting profile: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProxyProfile profile)
        {
            MessageBox.Show("Please select a profile to activate.", "No Profile Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ActivateProfileAsync(profile.Name);
    }

    private async void ProfilesListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is ProxyProfile profile)
        {
            await ActivateProfileAsync(profile.Name);
        }
    }

    private async Task ActivateProfileAsync(string profileName)
    {
        try
        {
            await _proxySwitcher.ActivateProfileAsync(profileName);
            InfoTextBlock.Text = $"Profile '{profileName}' activated successfully.";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Administrator privileges required"))
        {
            PromptForElevation("apply system proxy settings");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error activating profile: {ex.Message}", "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            InfoTextBlock.Text = $"Failed to activate profile: {ex.Message}";
        }
    }

    private async void DisableButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _proxySwitcher.DeactivateProxyAsync();
            InfoTextBlock.Text = "Proxy disabled successfully.";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Administrator privileges required"))
        {
            PromptForElevation("disable system proxy");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error disabling proxy: {ex.Message}", "Disable Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            InfoTextBlock.Text = $"Failed to disable proxy: {ex.Message}";
        }
    }

    private void PromptForElevation(string actionDescription)
    {
        var result = MessageBox.Show(
            $"Administrator privileges are required to {actionDescription}.\n\n" +
            "Do you want to restart Proxy Switcher with admin privileges?",
            "Administrator Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (ElevationHelper.RequestElevation())
            {
                if (System.Windows.Application.Current is App app)
                {
                    AllowClose = true;
                }
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                InfoTextBlock.Text = "Elevation request was cancelled.";
            }
        }
    }
}

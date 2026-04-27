using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ProxySwitcher.Data;
using ProxySwitcher.Services;

namespace ProxySwitcher;

/// <summary>
/// Interaction logic for App.xaml.
/// Hosts the application services, system tray icon and main window.
/// </summary>
public partial class App : System.Windows.Application
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _profilesMenu;
    private ToolStripMenuItem? _disableItem;
    private ToolStripMenuItem? _statusItem;

    private MainWindow? _mainWindow;

    private ProfileRepository? _repository;
    private ProfileManager? _profileManager;
    private RegistryProxyHandler? _registryHandler;
    private EnvironmentProxyHandler? _environmentHandler;
    private ProxySwitcher.Services.ProxySwitcher? _proxySwitcher;

    private bool _isExiting;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // Keep app alive even when no window is shown (tray-only).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize services (single instances shared across the app).
        _repository = new ProfileRepository();
        _registryHandler = new RegistryProxyHandler();
        _environmentHandler = new EnvironmentProxyHandler();
        _profileManager = new ProfileManager(_repository);
        _proxySwitcher = new ProxySwitcher.Services.ProxySwitcher(
            _profileManager, _registryHandler, _environmentHandler);

        // Create main window (not shown by default - app starts in tray).
        _mainWindow = new MainWindow(_profileManager, _proxySwitcher);

        // Setup system tray icon and context menu.
        SetupTrayIcon();

        // Subscribe to changes so tray reflects current state.
        _profileManager.ProfilesChanged += (_, _) => RefreshProfilesMenu();
        _profileManager.ActiveProfileChanged += (_, _) =>
        {
            RefreshProfilesMenu();
            UpdateTrayIcon();
        };
        _proxySwitcher.StatusChanged += (_, _) => UpdateTrayIcon();

        RefreshProfilesMenu();
        UpdateTrayIcon();
    }

    /// <summary>
    /// Brings the main window to front (or creates it if needed).
    /// </summary>
    public void ShowMainWindow()
    {
        if (_mainWindow == null)
            return;

        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Proxy Switcher"
        };

        _contextMenu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Proxy: Disabled") { Enabled = false };
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        _profilesMenu = new ToolStripMenuItem("Profiles");
        _contextMenu.Items.Add(_profilesMenu);

        _disableItem = new ToolStripMenuItem("Disable Proxy");
        _disableItem.Click += (_, _) => TrayDisableProxy();
        _contextMenu.Items.Add(_disableItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var openItem = new ToolStripMenuItem("Open Proxy Switcher");
        openItem.Click += (_, _) => ShowMainWindow();
        _contextMenu.Items.Add(openItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        _contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void RefreshProfilesMenu()
    {
        if (_profilesMenu == null || _profileManager == null)
            return;

        _profilesMenu.DropDownItems.Clear();

        var profiles = _profileManager.GetAllProfiles();
        var activeProfileName = _profileManager.GetActiveProfileName();

        if (profiles.Count == 0)
        {
            var noProfilesItem = new ToolStripMenuItem("(No profiles defined)") { Enabled = false };
            _profilesMenu.DropDownItems.Add(noProfilesItem);
        }
        else
        {
            foreach (var profile in profiles)
            {
                var profileItem = new ToolStripMenuItem(profile.ToString());
                var isActive = profile.Name == activeProfileName;
                profileItem.Checked = isActive;
                profileItem.CheckOnClick = false;

                var profileNameCapture = profile.Name; // capture for closure
                profileItem.Click += (_, _) => TrayActivateProfile(profileNameCapture);

                _profilesMenu.DropDownItems.Add(profileItem);
            }
        }

        if (_disableItem != null)
        {
            _disableItem.Enabled = !string.IsNullOrEmpty(activeProfileName);
        }
    }

    private void UpdateTrayIcon()
    {
        if (_notifyIcon == null || _proxySwitcher == null || _profileManager == null)
            return;

        try
        {
            var status = _proxySwitcher.GetStatus();
            var activeProfile = _profileManager.GetActiveProfile();

            string text;
            if (status.IsEnabled && activeProfile != null)
            {
                text = $"Proxy ON - {activeProfile.Name} ({status.ProxyHost}:{status.ProxyPort})";
                _notifyIcon.Icon = SystemIcons.Shield;
            }
            else if (status.IsEnabled)
            {
                text = $"Proxy ON - {status.ProxyHost}:{status.ProxyPort}";
                _notifyIcon.Icon = SystemIcons.Shield;
            }
            else
            {
                text = "Proxy: Disabled";
                _notifyIcon.Icon = SystemIcons.Application;
            }

            // NotifyIcon.Text has a 63-char limit on older Windows versions.
            if (text.Length > 63) text = text.Substring(0, 63);
            _notifyIcon.Text = text;

            if (_statusItem != null)
            {
                _statusItem.Text = (status.IsEnabled && activeProfile != null)
                    ? $"Active: {activeProfile.Name} ({status.ProxyHost}:{status.ProxyPort})"
                    : "Proxy: Disabled";
            }
        }
        catch (Exception ex)
        {
            _notifyIcon.Text = $"Proxy Switcher - Error";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void TrayActivateProfile(string profileName)
    {
        if (_proxySwitcher == null) return;

        try
        {
            _proxySwitcher.ActivateProfile(profileName);
            _notifyIcon?.ShowBalloonTip(
                2000,
                "Proxy Switcher",
                $"Profile '{profileName}' activated.",
                ToolTipIcon.Info);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Administrator privileges required"))
        {
            HandleElevationRequired("apply system proxy settings");
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                $"Error activating profile: {ex.Message}",
                "Activation Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void TrayDisableProxy()
    {
        if (_proxySwitcher == null) return;

        try
        {
            _proxySwitcher.DeactivateProxy();
            _notifyIcon?.ShowBalloonTip(
                2000,
                "Proxy Switcher",
                "Proxy disabled.",
                ToolTipIcon.Info);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Administrator privileges required"))
        {
            HandleElevationRequired("disable system proxy");
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                $"Error disabling proxy: {ex.Message}",
                "Disable Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void HandleElevationRequired(string actionDescription)
    {
        var result = System.Windows.Forms.MessageBox.Show(
            $"Administrator privileges are required to {actionDescription}.\n\n" +
            "Do you want to restart Proxy Switcher with admin privileges?",
            "Administrator Required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            if (ElevationHelper.RequestElevation())
            {
                ExitApplication();
            }
        }
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_mainWindow != null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
            _mainWindow = null;
        }

        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        base.OnExit(e);
    }
}

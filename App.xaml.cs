using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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
    private const string SingleInstanceMutexName = "Global\\ProxySwitcherSingleInstanceMutex";
    private const string RestoreEventName = "Global\\ProxySwitcherRestoreEvent";

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _profilesMenu;
    private ToolStripMenuItem? _disableItem;
    private ToolStripMenuItem? _statusItem;

    private Icon? _trayIcon;
    private Icon? _trayIconDisabled;

    private MainWindow? _mainWindow;

    private ProfileRepository? _repository;
    private ProfileManager? _profileManager;
    private RegistryProxyHandler? _registryHandler;
    private EnvironmentProxyHandler? _environmentHandler;
    private ProxySwitcher.Services.ProxySwitcher? _proxySwitcher;

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _restoreEvent;
    private RegisteredWaitHandle? _restoreWaitHandle;

    private bool _isExiting;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        if (!InitializeSingleInstance())
        {
            Shutdown();
            return;
        }

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

    private bool InitializeSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                try
                {
                    _restoreEvent = EventWaitHandle.OpenExisting(RestoreEventName);
                    _restoreEvent.Set();
                }
                catch
                {
                    // Existing instance may not yet have created the event.
                }

                return false;
            }

            _restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
            _restoreWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _restoreEvent,
                (_, _) => Dispatcher.InvokeAsync(ShowMainWindow),
                null,
                Timeout.Infinite,
                false);

            return true;
        }
        catch
        {
            return true;
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = LoadAppIcon() ?? SystemIcons.Application;
        _trayIconDisabled = CreateDisabledIcon(_trayIcon) ?? _trayIcon;

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
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

    private Icon? LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico");
            var resourceStream = System.Windows.Application.GetResourceStream(uri);

            if (resourceStream != null)
                return new Icon(resourceStream.Stream);

            return null;
        }
        catch
        {
            return null;
        }
        /*
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
                return null;

var resourceName = entryAssembly.GetManifestResourceNames()
    .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(resourceName))
            {
                using var stream = entryAssembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                    return new Icon(stream);
            }

            var executablePath = entryAssembly.Location;
            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                return Icon.ExtractAssociatedIcon(executablePath);

            var assetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(assetPath))
                return new Icon(assetPath);

            return null;
        }
        catch
        {
            return null;
        }
        */

    }

    private Icon? CreateDisabledIcon(Icon? sourceIcon)
    {
        if (sourceIcon == null)
            return null;

        using var bitmap = sourceIcon.ToBitmap();
        using var grayBitmap = new Bitmap(bitmap.Width, bitmap.Height);
        using (var graphics = Graphics.FromImage(grayBitmap))
        {
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);
            graphics.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
        }

        var handle = grayBitmap.GetHicon();
        try
        {
            var icon = Icon.FromHandle(handle);
            var clonedIcon = (Icon)icon.Clone();
            icon.Dispose();
            return clonedIcon;
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

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
            if (status.IsEnabled)
            {
                text = activeProfile != null
                    ? $"Proxy ON - {activeProfile.Name} ({status.ProxyHost}:{status.ProxyPort})"
                    : $"Proxy ON - {status.ProxyHost}:{status.ProxyPort}";
                _notifyIcon.Icon = _trayIcon ?? SystemIcons.Application;
            }
            else
            {
                text = "Proxy: Disabled";
                _notifyIcon.Icon = _trayIconDisabled ?? _trayIcon ?? SystemIcons.Application;
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

    private async void TrayActivateProfile(string profileName)
    {
        if (_proxySwitcher == null) return;

        try
        {
            await _proxySwitcher.ActivateProfileAsync(profileName);
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

    private async void TrayDisableProxy()
    {
        if (_proxySwitcher == null) return;

        try
        {
            await _proxySwitcher.DeactivateProxyAsync();
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

        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayIconDisabled?.Dispose();
        _trayIconDisabled = null;

        if (_mainWindow != null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
            _mainWindow = null;
        }

        _restoreWaitHandle?.Unregister(null);
        _restoreEvent?.Close();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

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

        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayIconDisabled?.Dispose();
        _trayIconDisabled = null;

        _restoreWaitHandle?.Unregister(null);
        _restoreEvent?.Close();
        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using MaxwellBoost.Config;
using MaxwellBoost.CoreAudio;
using MaxwellBoost.Logging;

namespace MaxwellBoost.UI
{
    public class TrayMainForm : Form
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MaxwellBoost";

        private readonly AppSettings _settings;
        private readonly DailyRotatingLogger _logger;
        private readonly AudioDeviceWatcher _watcher;

        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _statusMenuItem;
        private readonly ToolStripMenuItem _volumeMenuItem;
        private readonly ToolStripGainSlider _gainSlider;
        private readonly ToolStripMenuItem _notificationsMenuItem;
        private readonly ToolStripMenuItem _startupMenuItem;

        private FileSystemWatcher? _configWatcher;
        private System.Threading.Timer? _configDebounceTimer;

        public TrayMainForm(
            AppSettings settings,
            DailyRotatingLogger logger,
            AudioDeviceWatcher watcher)
        {
            _settings = settings;
            _logger = logger;
            _watcher = watcher;

            // Form properties for pure background hosting
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Size = new Size(0, 0);
            Location = new Point(-3000, -3000);

            // Force native Win32 window handle creation immediately
            _ = this.Handle;

            _contextMenu = new ContextMenuStrip();

            // 1. Title Item
            var titleItem = new ToolStripMenuItem("MaxwellBoost v1.0")
            {
                Font = new Font(Control.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                Enabled = false
            };
            _contextMenu.Items.Add(titleItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // 2. Status & Volume Items
            _statusMenuItem = new ToolStripMenuItem("Status: Initializing...") { Enabled = false };
            _volumeMenuItem = new ToolStripMenuItem("Volume: Checking...") { Enabled = false };
            _contextMenu.Items.Add(_statusMenuItem);
            _contextMenu.Items.Add(_volumeMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // 3. Interactive Gain Slider (0 to 40 dB)
            _gainSlider = new ToolStripGainSlider(_settings, OnSliderGainChanged);
            _contextMenu.Items.Add(_gainSlider);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // 4. Quick Actions
            var refreshItem = new ToolStripMenuItem("⚡ Re-apply Boost Now", null, (s, e) =>
            {
                _logger.Info("Manual boost re-apply triggered by user from tray menu.");
                _settings.Reload();
                _watcher.SyncCurrentState(logStateChanges: true);
            });
            _contextMenu.Items.Add(refreshItem);

            var openLogItem = new ToolStripMenuItem("📄 Open Log File", null, (s, e) => OpenLogFile());
            _contextMenu.Items.Add(openLogItem);

            var openConfigItem = new ToolStripMenuItem("⚙️ Open Settings", null, (s, e) => OpenConfigFile());
            _contextMenu.Items.Add(openConfigItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 5. Toggles (Notifications & Startup)
            _notificationsMenuItem = new ToolStripMenuItem("Show Toast Notifications", null, (s, e) => ToggleNotifications());
            _notificationsMenuItem.Checked = _settings.ShowNotifications;
            _contextMenu.Items.Add(_notificationsMenuItem);

            _startupMenuItem = new ToolStripMenuItem("Run on Windows Startup", null, (s, e) => ToggleStartup());
            _startupMenuItem.Checked = IsStartupEnabled();
            _contextMenu.Items.Add(_startupMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 6. Exit
            var exitItem = new ToolStripMenuItem("❌ Exit MaxwellBoost", null, (s, e) => ExitApp());
            _contextMenu.Items.Add(exitItem);

            // Initialize NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Visible = true,
                Text = TruncateTip("MaxwellBoost (Initializing...)"),
                Icon = Icons.CreateMicrophoneIcon(isConnected: false)
            };

            _notifyIcon.DoubleClick += (s, e) => OpenLogFile();

            // Subscribe to watcher events with thread-safe UI invocation
            _watcher.OnDeviceBoosted += (dev, vol, isInitial) => SafeInvoke(() => HandleDeviceBoosted(dev, vol, isInitial));
            _watcher.OnDeviceDisconnected += (devName) => SafeInvoke(() => HandleDeviceDisconnected(devName));
            _watcher.OnStatusChanged += (status) => SafeInvoke(() => HandleStatusChanged(status));

            // Start watching appsettings.json for hot-reload
            SetupConfigFileWatcher();

            _logger.Info("Tray application initialized.");

            // Initial sync to set status icon
            _watcher.TriggerSync(delayMs: 200);
        }

        private void OnSliderGainChanged(double newGain)
        {
            if (Math.Abs(_settings.GainDb - newGain) < 0.01) return;

            _logger.Info($"User adjusted gain slider in quick menu to +{newGain:0.#} dB.");
            _settings.GainDb = newGain;
            _settings.Save();
            _watcher.SyncCurrentState(logStateChanges: true);
        }

        private void ToggleNotifications()
        {
            _settings.ShowNotifications = !_settings.ShowNotifications;
            _notificationsMenuItem.Checked = _settings.ShowNotifications;
            _settings.Save();
            _logger.Info($"Toast notifications toggled to: {(_settings.ShowNotifications ? "Enabled" : "Disabled")}");
        }

        private void SetupConfigFileWatcher()
        {
            try
            {
                var configPath = AppSettings.GetConfigFilePath();
                var configDir = Path.GetDirectoryName(configPath);
                var configFileName = Path.GetFileName(configPath);

                if (!string.IsNullOrEmpty(configDir) && Directory.Exists(configDir))
                {
                    _configWatcher = new FileSystemWatcher(configDir, configFileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };

                    _configWatcher.Changed += (s, e) => OnConfigFileChanged();
                    _configWatcher.Created += (s, e) => OnConfigFileChanged();
                    _logger.Info($"Enabled automatic settings hot-reload watcher on: {configPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not initialize config file watcher: {ex.Message}");
            }
        }

        private void OnConfigFileChanged()
        {
            _configDebounceTimer?.Dispose();
            _configDebounceTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (_settings.Reload())
                    {
                        _logger.Info($"Detected change in appsettings.json. Hot-reloaded settings! Target Gain: +{_settings.GainDb} dB.");
                        SafeInvoke(() =>
                        {
                            _gainSlider.SliderControl.SetGain(_settings.GainDb);
                            _notificationsMenuItem.Checked = _settings.ShowNotifications;
                        });
                        _watcher.SyncCurrentState(logStateChanges: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Hot-reload error: {ex.Message}");
                }
            }, null, 300, Timeout.Infinite);
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed || Disposing) return;

                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("UI invoke error", ex);
            }
        }

        private void HandleDeviceBoosted(AudioDeviceInfo device, float volume, bool isInitialConnect)
        {
            if (_notifyIcon == null || IsDisposed) return;

            try
            {
                var oldIcon = _notifyIcon.Icon;
                _notifyIcon.Icon = Icons.CreateMicrophoneIcon(isConnected: true);
                oldIcon?.Dispose();

                _notifyIcon.Text = TruncateTip($"MaxwellBoost: Active (+{_settings.GainDb:0.#} dB)");
                _statusMenuItem.Text = $"Status: Connected (+{_settings.GainDb:0.#} dB)";
                _volumeMenuItem.Text = $"Volume: {volume:P0} (Enforced)";
                _gainSlider.SliderControl.SetGain(_settings.GainDb);

                // ONLY show toast notification when the device actually performs an initial connection, NEVER on gain changes
                if (isInitialConnect && _settings.ShowNotifications)
                {
                    _notifyIcon.ShowBalloonTip(
                        3000,
                        "Audeze Maxwell Connected",
                        $"Microphone active (+{_settings.GainDb:0.#} dB)",
                        ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Tray update error on boost", ex);
            }
        }

        private void HandleDeviceDisconnected(string deviceName)
        {
            if (_notifyIcon == null || IsDisposed) return;

            try
            {
                var oldIcon = _notifyIcon.Icon;
                _notifyIcon.Icon = Icons.CreateMicrophoneIcon(isConnected: false);
                oldIcon?.Dispose();

                _notifyIcon.Text = TruncateTip("MaxwellBoost: Disconnected (Standby)");
                _statusMenuItem.Text = "Status: Disconnected (Standby)";
                _volumeMenuItem.Text = "Volume: N/A";

                // ONLY show toast notification on disconnection if explicitly enabled
                if (_settings.ShowNotifications)
                {
                    _notifyIcon.ShowBalloonTip(
                        3000,
                        "Audeze Maxwell Disconnected",
                        "Headset is off or disconnected. Watching for reconnection...",
                        ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Tray update error on disconnect", ex);
            }
        }

        private void HandleStatusChanged(string status)
        {
            try
            {
                _statusMenuItem.Text = $"Status: {status}";
            }
            catch
            {
                // Ignored
            }
        }

        private static string TruncateTip(string text)
        {
            if (string.IsNullOrEmpty(text)) return "MaxwellBoost";
            return text.Length > 63 ? text.Substring(0, 63) : text;
        }

        private void OpenLogFile()
        {
            try
            {
                var logPath = _logger.LogFilePath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open log file:\n{ex.Message}", "MaxwellBoost", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenConfigFile()
        {
            try
            {
                var configPath = AppSettings.GetConfigFilePath();
                Process.Start(new ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open settings file:\n{ex.Message}", "MaxwellBoost", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
                if (key == null) return;

                if (IsStartupEnabled())
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    _startupMenuItem.Checked = false;
                    _logger.Info("Disabled automatic Windows startup.");
                }
                else
                {
                    var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                    key.SetValue(AppName, $"\"{exePath}\"");
                    _startupMenuItem.Checked = true;
                    _logger.Info($"Enabled automatic Windows startup -> {exePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to update Windows startup registry key", ex);
                MessageBox.Show($"Failed to modify startup setting:\n{ex.Message}", "MaxwellBoost", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExitApp()
        {
            _logger.Info("MaxwellBoost is exiting via tray menu.");
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _watcher.Dispose();
            _configWatcher?.Dispose();
            _configDebounceTimer?.Dispose();
            Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Icon?.Dispose();
                _notifyIcon.Dispose();
                _contextMenu.Dispose();
                _watcher.Dispose();
                _configWatcher?.Dispose();
                _configDebounceTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

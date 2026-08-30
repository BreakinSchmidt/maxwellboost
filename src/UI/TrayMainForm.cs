using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private readonly ToolStripMenuItem _startupMenuItem;

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

            _contextMenu = new ContextMenuStrip();

            // 1. Title Item
            var titleItem = new ToolStripMenuItem("MaxwellBoost v1.0")
            {
                Font = new Font(Control.DefaultFont, FontStyle.Bold),
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

            // 3. Actions
            var refreshItem = new ToolStripMenuItem("⚡ Re-apply Boost Now", null, (s, e) =>
            {
                _logger.Info("Manual boost re-apply triggered by user from tray menu.");
                _watcher.SyncCurrentState(logStateChanges: true);
            });
            _contextMenu.Items.Add(refreshItem);

            var openLogItem = new ToolStripMenuItem("📄 Open Log File", null, (s, e) => OpenLogFile());
            _contextMenu.Items.Add(openLogItem);

            var openConfigItem = new ToolStripMenuItem("⚙️ Open Settings", null, (s, e) => OpenConfigFile());
            _contextMenu.Items.Add(openConfigItem);

            // 4. Startup Toggle
            _startupMenuItem = new ToolStripMenuItem("Run on Windows Startup", null, (s, e) => ToggleStartup());
            _startupMenuItem.Checked = IsStartupEnabled();
            _contextMenu.Items.Add(_startupMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 5. Exit
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

            FormClosing += (s, e) =>
            {
                _logger.Info($"TrayMainForm FormClosing triggered. CloseReason: {e.CloseReason}, Cancel: {e.Cancel}");
            };

            FormClosed += (s, e) =>
            {
                _logger.Info($"TrayMainForm FormClosed triggered. CloseReason: {e.CloseReason}");
            };

            // Subscribe to watcher events with thread-safe UI invocation
            _watcher.OnDeviceBoosted += (dev, vol) => SafeInvoke(() => HandleDeviceBoosted(dev, vol));
            _watcher.OnDeviceDisconnected += (devName) => SafeInvoke(() => HandleDeviceDisconnected(devName));
            _watcher.OnStatusChanged += (status) => SafeInvoke(() => HandleStatusChanged(status));

            _logger.Info("Tray application initialized.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _logger.Info($"TrayMainForm native handle created: 0x{Handle:X8}");
            _watcher.TriggerSync(delayMs: 200);
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

        private void HandleDeviceBoosted(AudioDeviceInfo device, float volume)
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

                if (_settings.ShowNotifications)
                {
                    _notifyIcon.ShowBalloonTip(
                        3000,
                        "Audeze Maxwell Connected",
                        $"Amplified microphone by +{_settings.GainDb:0.#} dB (Volume: {volume:P0})",
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
            }
            base.Dispose(disposing);
        }
    }
}

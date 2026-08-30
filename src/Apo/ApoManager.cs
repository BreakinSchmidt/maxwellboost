using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using MaxwellBoost.Config;
using MaxwellBoost.CoreAudio;
using MaxwellBoost.Logging;

namespace MaxwellBoost.Apo
{
    public class ApoManager
    {
        private const string EqualizerApoClsid = "{EACD2258-FCAC-4FF4-B36D-419E924A6D79}";
        private const string SfxKeyName = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5";
        private const string LfxKeyName = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},1";
        private static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

        private readonly AppSettings _settings;
        private readonly DailyRotatingLogger _logger;

        public ApoManager(AppSettings settings, DailyRotatingLogger logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public bool EnsureApoConfig(string deviceName, double gainDb)
        {
            try
            {
                var configPath = _settings.EqualizerApoConfigPath;
                var configDir = Path.GetDirectoryName(configPath);
                if (string.IsNullOrEmpty(configDir) || !Directory.Exists(configDir))
                {
                    _logger.Warn($"Equalizer APO config directory not found: {configDir}");
                    return false;
                }

                var deviceLine = $"Device: \"{deviceName}\" capture";
                var preampLine = string.Format(CultureInfo.InvariantCulture, "Preamp: {0:0.0} dB", gainDb);
                var targetBlock = $"{deviceLine}\n{preampLine}";

                string existingContent = string.Empty;
                if (File.Exists(configPath))
                {
                    existingContent = File.ReadAllText(configPath);
                }

                // Check if device block already has exact target preamp
                var exactRegex = new Regex($@"Device:\s*""{Regex.Escape(deviceName)}""\s+capture\s*[\r\n]+\s*{Regex.Escape(preampLine)}", RegexOptions.IgnoreCase);
                if (exactRegex.IsMatch(existingContent))
                {
                    _logger.Debug($"Equalizer APO config.txt already has [{preampLine}] for [{deviceName}].");
                    return true;
                }

                // Pattern to match Device line followed by any Preamp line
                var deviceWithPreampPattern = new Regex($@"(Device:\s*""{Regex.Escape(deviceName)}""\s+capture\s*[\r\n]+)\s*Preamp:[^\r\n]*", RegexOptions.IgnoreCase);

                string newContent;
                if (deviceWithPreampPattern.IsMatch(existingContent))
                {
                    newContent = deviceWithPreampPattern.Replace(existingContent, $"$1{preampLine}");
                }
                else if (existingContent.Contains(deviceLine, StringComparison.OrdinalIgnoreCase))
                {
                    // Device line is present but without immediate Preamp line
                    var deviceOnlyPattern = new Regex($@"Device:\s*""{Regex.Escape(deviceName)}""\s+capture", RegexOptions.IgnoreCase);
                    newContent = deviceOnlyPattern.Replace(existingContent, targetBlock, 1);
                }
                else
                {
                    // Prepend target block to top of config file
                    newContent = string.IsNullOrWhiteSpace(existingContent)
                        ? targetBlock + Environment.NewLine
                        : $"{targetBlock}\n\n{existingContent.Trim()}";
                }

                File.WriteAllText(configPath, newContent, Encoding.UTF8);
                _logger.Info($"Updated Equalizer APO config at {configPath} with [{preampLine}] for device [{deviceName}].");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to update Equalizer APO config file", ex);
                return false;
            }
        }

        public bool EnsureRegistryHooks(AudioDeviceInfo deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceInfo.EndpointGuid))
            {
                _logger.Warn("Cannot verify registry hooks: Endpoint GUID is empty.");
                return false;
            }

            var guid = deviceInfo.EndpointGuid;
            var fxKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\{guid}\FxProperties";
            var childApoPath = $@"SOFTWARE\EqualizerAPO\Child APOs\{guid}";

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(fxKeyPath, writable: false);
                if (key != null)
                {
                    var sfxVal = key.GetValue(SfxKeyName)?.ToString();
                    var lfxVal = key.GetValue(LfxKeyName)?.ToString();

                    var hasApo = string.Equals(sfxVal, EqualizerApoClsid, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(lfxVal, EqualizerApoClsid, StringComparison.OrdinalIgnoreCase);

                    if (hasApo)
                    {
                        _logger.Info($"APO registry hooks verified for endpoint {guid}.");
                    }
                    else
                    {
                        _logger.Warn($"APO hook not registered in FxProperties for endpoint {guid}. Attempting registration...");
                        TryWriteRegistryHooks(fxKeyPath, childApoPath);
                    }
                }
                else
                {
                    _logger.Warn($"FxProperties registry key not found for {guid}.");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not inspect registry keys: {ex.Message}");
                return false;
            }
        }

        private void TryWriteRegistryHooks(string fxKeyPath, string childApoPath)
        {
            try
            {
                using var fxKey = Registry.LocalMachine.OpenSubKey(fxKeyPath, writable: true);
                if (fxKey != null)
                {
                    fxKey.SetValue(SfxKeyName, EqualizerApoClsid, RegistryValueKind.String);
                    _logger.Info($"Successfully wrote {SfxKeyName} to {fxKeyPath}");
                }

                using var childKey = Registry.LocalMachine.CreateSubKey(childApoPath);
                if (childKey != null)
                {
                    childKey.SetValue("AllowSilentBufferModification", "false", RegistryValueKind.String);
                    childKey.SetValue("Version", 2, RegistryValueKind.DWord);
                    _logger.Info($"Successfully ensured Child APO registry key at {childApoPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Note: Registry write requires administrator privileges: {ex.Message}");
            }
        }

        public bool WarmupEndpointStream(IMMDevice device)
        {
            try
            {
                var iid = IID_IAudioClient;
                var hr = device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var pUnk);
                if (hr == 0 && pUnk is IAudioClient audioClient)
                {
                    hr = audioClient.GetMixFormat(out var pFormat);
                    if (hr == 0 && pFormat != IntPtr.Zero)
                    {
                        try
                        {
                            var emptyGuid = Guid.Empty;
                            hr = audioClient.Initialize(0, 0, 1000000, 0, pFormat, ref emptyGuid);
                            if (hr == 0)
                            {
                                audioClient.Start();
                                System.Threading.Thread.Sleep(50);
                                audioClient.Stop();
                                audioClient.Reset();
                                _logger.Debug("Audio capture stream warmed up successfully (APO pipeline bound).");
                                return true;
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pFormat);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Stream warmup notice (non-fatal): {ex.Message}");
            }

            return false;
        }
    }
}

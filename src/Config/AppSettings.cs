using System;
using System.IO;
using System.Text.Json;

namespace MaxwellBoost.Config
{
    public class AppSettings
    {
        public string DeviceNameFilter { get; set; } = "Chat-Audeze Maxwell";
        public double GainDb { get; set; } = 20.0;
        public bool EnforceVolume { get; set; } = true;
        public float TargetVolumeScalar { get; set; } = 1.0f;
        public string LogDirectory { get; set; } = @"C:\logs";
        public string LogFileName { get; set; } = "maxwell.log";
        public int LogRetentionDays { get; set; } = 7;
        public bool ShowNotifications { get; set; } = false;
        public int PollingFallbackSeconds { get; set; } = 10;
        public string EqualizerApoConfigPath { get; set; } = @"C:\Program Files\EqualizerAPO\config\config.txt";

        public static string GetConfigFilePath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var localConfig = Path.Combine(baseDir, "Config", "appsettings.json");
            if (File.Exists(localConfig))
            {
                return localConfig;
            }

            var rootConfig = Path.Combine(baseDir, "appsettings.json");
            return rootConfig;
        }

        public static AppSettings Load()
        {
            try
            {
                var filePath = GetConfigFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // Fallback to defaults if read fails
            }

            return new AppSettings();
        }

        public bool Reload()
        {
            try
            {
                var filePath = GetConfigFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                    if (settings != null)
                    {
                        CopyFrom(settings);
                        return true;
                    }
                }
            }
            catch
            {
                // Read error
            }
            return false;
        }

        public void CopyFrom(AppSettings other)
        {
            DeviceNameFilter = other.DeviceNameFilter;
            GainDb = other.GainDb;
            EnforceVolume = other.EnforceVolume;
            TargetVolumeScalar = other.TargetVolumeScalar;
            LogDirectory = other.LogDirectory;
            LogFileName = other.LogFileName;
            LogRetentionDays = other.LogRetentionDays;
            ShowNotifications = other.ShowNotifications;
            PollingFallbackSeconds = other.PollingFallbackSeconds;
            EqualizerApoConfigPath = other.EqualizerApoConfigPath;
        }

        public void Save()
        {
            try
            {
                var filePath = GetConfigFilePath();
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Ignore save errors if locked
            }
        }
    }
}

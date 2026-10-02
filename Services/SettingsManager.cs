using System;
using System.IO;
using System.Text.Json;
using ProtonVpnGenerator.Models;

namespace ProtonVpnGenerator.Services
{
    public class SettingsManager
    {
        private readonly string _settingsFilePath;

        public SettingsManager(string? customPath = null)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                _settingsFilePath = customPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "ProtonVpnGenerator");
                Directory.CreateDirectory(folder);
                _settingsFilePath = Path.Combine(folder, "settings.json");
            }
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch
            {
                // Fallback to default
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}

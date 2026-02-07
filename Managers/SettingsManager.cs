using System;
using System.IO;
using System.Text.Json;

namespace PianoApp.Managers
{
    /// <summary>
    /// Manages application settings persistence.
    /// Settings are saved to a JSON file next to the executable.
    /// </summary>
    public class SettingsManager
    {
        private const string SETTINGS_FILE = "PianoApp.settings.json";
        
        private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, SETTINGS_FILE);

        /// <summary>
        /// Application settings.
        /// </summary>
        public class AppSettings
        {
            public string SelectedSamplePack { get; set; } = "Default";
            public int Transpose { get; set; } = 0;
        }

        private static AppSettings? _cachedSettings;

        /// <summary>
        /// Loads settings from disk. Returns default settings if file doesn't exist.
        /// </summary>
        public static AppSettings Load()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _cachedSettings = new AppSettings();
                }
            }
            catch
            {
                _cachedSettings = new AppSettings();
            }

            return _cachedSettings;
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                _cachedSettings = settings;
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore save errors (e.g., read-only location)
            }
        }

        /// <summary>
        /// Updates just the selected sample pack and saves.
        /// </summary>
        public static void SaveSelectedSamplePack(string packName)
        {
            var settings = Load();
            settings.SelectedSamplePack = packName;
            Save(settings);
        }

        /// <summary>
        /// Updates just the transpose value and saves.
        /// </summary>
        public static void SaveTranspose(int transpose)
        {
            var settings = Load();
            settings.Transpose = transpose;
            Save(settings);
        }
    }
}

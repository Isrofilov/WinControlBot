using System;
using System.IO;
using System.Text.Json;

namespace WinControlBot
{
    public class AppSettings
    {
        public string BotToken { get; set; } = "";
        public string AuthorizedUsers { get; set; } = "";
        public bool AutoStartEnabled { get; set; } = false;
        public string Language { get; set; } = "";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinControlBot");
        
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");
        
        private static AppSettings? _currentSettings;
        
        public static AppSettings Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    Load();
                }
                return _currentSettings!;
            }
        }

        static SettingsManager()
        {
            // Create folder if it does not exist
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _currentSettings = new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // In case of an error, create default settings
                _currentSettings = new AppSettings();
                // Logging the error for debugging
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                if (_currentSettings == null) return;

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                // Logging the error for debugging
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
                throw; // Pass the exception to the UI for handling
            }
        }

        public static string GetSettingsFolder()
        {
            return SettingsFolder;
        }

        public static string GetSettingsFilePath()
        {
            return SettingsFile;
        }
    }
}
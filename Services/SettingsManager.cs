using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace WinControlBot
{
    public class AppSettings
    {
        public string BotToken { get; set; } = "";
        public string AuthorizedUsers { get; set; } = "";
        public bool AutoStartEnabled { get; set; } = false;
        public string Language { get; set; } = "";
    }

    // Class for storing encrypted settings
    internal class EncryptedSettings
    {
        public string EncryptedBotToken { get; set; } = "";
        public string EncryptedAuthorizedUsers { get; set; } = "";
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
        
        // Entropy for extra protection
        private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("WinControlBot_2");

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
                    var encryptedSettings = JsonSerializer.Deserialize<EncryptedSettings>(json) ?? new EncryptedSettings();
                    
                    _currentSettings = new AppSettings
                    {
                        BotToken = DecryptString(encryptedSettings.EncryptedBotToken),
                        AuthorizedUsers = DecryptString(encryptedSettings.EncryptedAuthorizedUsers),
                        AutoStartEnabled = encryptedSettings.AutoStartEnabled,
                        Language = encryptedSettings.Language
                    };
                }
                else
                {
                    _currentSettings = new AppSettings();
                }
            }
            catch (Exception ex)
            {
                _currentSettings = new AppSettings();
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                if (_currentSettings == null) return;

                var encryptedSettings = new EncryptedSettings
                {
                    EncryptedBotToken = EncryptString(_currentSettings.BotToken),
                    EncryptedAuthorizedUsers = EncryptString(_currentSettings.AuthorizedUsers),
                    AutoStartEnabled = _currentSettings.AutoStartEnabled,
                    Language = _currentSettings.Language
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(encryptedSettings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        private static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return "";

            try
            {
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainTextBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encryption error: {ex.Message}");
                return plainText; // Return the original text in case of an error
            }
        }

        private static string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return "";

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Decryption error: {ex.Message}");
                return ""; // Return an empty string if an error occurs
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

        // Method to check if settings are already encrypted
        public static bool AreSettingsEncrypted()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return false;

                string json = File.ReadAllText(SettingsFile);
                var encryptedSettings = JsonSerializer.Deserialize<EncryptedSettings>(json);
                
                // Check if there are encrypted fields
                return !string.IsNullOrEmpty(encryptedSettings?.EncryptedBotToken) ||
                       !string.IsNullOrEmpty(encryptedSettings?.EncryptedAuthorizedUsers);
            }
            catch
            {
                return false;
            }
        }
    }
}
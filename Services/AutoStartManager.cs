using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;

namespace WinControlBot
{
    public static class AutoStartManager
    {
        private const string APP_NAME = "WinControlBot";
        private static readonly string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private static string GetApplicationPath()
        {
            return Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;
        }

        public static bool IsAutoStartEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false);
                    string? value = key?.GetValue(APP_NAME)?.ToString();
                    
                    // Check not only the presence of the key, but also the correctness of the path
                    if (!string.IsNullOrEmpty(value))
                    {
                        string currentPath = GetApplicationPath();
                        return value.Contains(currentPath);
                    }
                    
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
                if (key == null) return;

                if (enable)
                {
                    string appPath = GetApplicationPath();
                    if (string.IsNullOrEmpty(appPath))
                        throw new Exception("Could not determine application path");

                    // Add -autostart parameter for autostart in tray
                    string startupPath = $"\"{appPath}\" -autostart";
                    key.SetValue(APP_NAME, startupPath);
                }
                else
                {
                    key.DeleteValue(APP_NAME, false);
                }

                // Update settings in file
                var settings = SettingsManager.Current;
                settings.AutoStartEnabled = enable;
                SettingsManager.Save();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error changing autostart: {ex.Message}");
            }
        }
        
        public static string GetCurrentRegistryValue()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false);
                return key?.GetValue(APP_NAME)?.ToString() ?? "Not found";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public static string GetDetectedPath()
        {
            return GetApplicationPath();
        }

        /// <summary>
        /// Synchronizes autostart state between registry and settings
        /// </summary>
        public static void SynchronizeState()
        {
            try
            {
                bool registryState = IsAutoStartEnabled;
                var settings = SettingsManager.Current;
                
                // If states don't match, registry takes priority
                if (settings.AutoStartEnabled != registryState)
                {
                    settings.AutoStartEnabled = registryState;
                    SettingsManager.Save();
                }
            }
            catch (Exception)
            {
                // Ignore synchronization errors
            }
        }
    }
}
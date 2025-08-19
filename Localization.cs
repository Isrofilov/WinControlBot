using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace WinControlBot.Localization
{
    /// <summary>
    /// Used in XAML: {loc:Loc SomeKey}
    /// Takes a string from Resources.Strings.resx
    /// </summary>
    public class LocExtension : MarkupExtension
    {
        private readonly string _key;
        private static readonly ResourceManager ResManager = Resources.Strings.ResourceManager;

        public LocExtension(string key) => _key = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return ResManager.GetString(_key, CultureInfo.CurrentUICulture) ?? $"!{_key}!";
        }
    }

    /// <summary>
    /// Allows you to switch languages while working.
    /// Used in XAML via binding:
    /// Text="{Binding [Settings_Title], Source={x:Static loc:LocalizationManager.Instance}}"
    /// </summary>
    public class LocalizationManager : INotifyPropertyChanged
    {
        public static LocalizationManager Instance { get; } = new();

        private static readonly ResourceManager ResManager = Resources.Strings.ResourceManager;

        /// <summary>
        /// Indexer for accessing resource strings
        /// </summary>
        public string this[string key] => ResManager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";

        /// <summary>
        /// Changing language while working
        /// </summary>
        public void ChangeCulture(string cultureCode)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
            CultureInfo.CurrentCulture   = new CultureInfo(cultureCode);
            
            // Save the selected language in the settings
            var settings = SettingsManager.Current;
            settings.Language = cultureCode;
            SettingsManager.Save();
            
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); // we update all bindings
        }

        /// <summary>
        /// Sets the default language on startup.
        /// </summary>
        public void InitializeDefaultLanguage()
        {
            var settings = SettingsManager.Current;
            
            // If the language is not specified in the settings, we define it by default
            if (string.IsNullOrEmpty(settings.Language))
            {
                string defaultLanguage = GetDefaultLanguage();
                settings.Language = defaultLanguage;
                SettingsManager.Save();
            }
            
            ChangeCultureInternal(settings.Language);
        }

        /// <summary>
        /// Determines the default language based on the system locale
        /// </summary>
        private string GetDefaultLanguage()
        {
            var currentCulture = CultureInfo.CurrentCulture;
            
            // Post-Soviet countries - Russian language
            string[] postSovietCountries = { "RU", "BY", "KZ", "KG", "TJ", "TM", "UZ", "AM", "AZ", "GE", "MD", "UA" };
            
            foreach (string country in postSovietCountries)
            {
                if (currentCulture.Name.EndsWith(country) || currentCulture.TwoLetterISOLanguageName == "ru")
                {
                    return "ru-RU";
                }
            }
            
            // Default is English
            return "en-US";
        }

        /// <summary>
        /// Internal method for changing culture without saving to settings
        /// </summary>
        private void ChangeCultureInternal(string cultureCode)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
            CultureInfo.CurrentCulture = new CultureInfo(cultureCode);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
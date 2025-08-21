using System;
using System.Linq;
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot.Services
{
    public class SettingsService
    {
        private readonly UIService _uiService;
        private readonly BotService _botService;
        
        private bool _isTokenVisible;
        private string _realToken = string.Empty;

        public event Action<string>? LogRequested;

        public bool IsTokenVisible => _isTokenVisible;
        public string RealToken => _realToken;

        public SettingsService(UIService uiService, BotService botService)
        {
            _uiService = uiService;
            _botService = botService;
        }

        public void LoadSettings()
        {
            try
            {
                var settings = SettingsManager.Current;
                
                if (!string.IsNullOrEmpty(settings.BotToken))
                {
                    _realToken = settings.BotToken;
                    _botService.Token = settings.BotToken;
                }

                string authorizedUsers = settings.AuthorizedUsers ?? string.Empty;
                if (!string.IsNullOrEmpty(authorizedUsers))
                {
                    _botService.AuthorizedUsers = authorizedUsers
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => long.TryParse(x.Trim(), out var id) ? id : 0)
                        .Where(x => x > 0)
                        .ToArray();
                }

                _uiService.LoadUISettings(_realToken, authorizedUsers);

                // Synchronize autostart state
                AutoStartManager.SynchronizeState();

                LogRequested?.Invoke(string.Format(Loc.Instance["Window_SettingsLoaded"]));
                LogRequested?.Invoke(string.Format(Loc.Instance["Window_SettingsPath"], SettingsManager.GetSettingsFilePath()));
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_SettingsLoad"], ex.Message));
            }
        }

        public void SaveSettings()
        {
            try
            {
                string token = GetCurrentToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    LogRequested?.Invoke(string.Format(Loc.Instance["Validation_EmptyTokenWarning"]));
                    return;
                }

                string authorizedUsers = _uiService.GetAuthorizedUsersFromUI();

                var settings = SettingsManager.Current;
                settings.BotToken = token;
                settings.AuthorizedUsers = authorizedUsers;
                SettingsManager.Save();

                _botService.Token = token;
                _realToken = token;
                _botService.AuthorizedUsers = authorizedUsers
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => long.TryParse(x.Trim(), out var id) ? id : 0)
                    .Where(x => x > 0)
                    .ToArray();

                LogRequested?.Invoke(string.Format(Loc.Instance["Window_SettingsSaved"]));
                LogRequested?.Invoke(string.Format(Loc.Instance["Window_TokenSaved"], token[..Math.Min(10, token.Length)]));
                LogRequested?.Invoke(string.Format(Loc.Instance["Window_UserCount"], _botService.AuthorizedUsers.Length));
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_SettingsSave"], ex.Message));
            }
        }

        public void ToggleTokenVisibility()
        {
            _isTokenVisible = !_isTokenVisible;
            
            // If making visible, get the current text from UI (user might have typed something new)
            if (_isTokenVisible)
            {
                string currentText = _uiService.GetCurrentTokenFromUI();
                if (!string.IsNullOrEmpty(currentText) && !currentText.All(c => c == '●'))
                {
                    _realToken = currentText;
                }
            }
            
            _uiService.SetTokenVisibility(_isTokenVisible, _realToken);
        }

        private string GetCurrentToken()
        {
            if (_isTokenVisible)
            {
                return _uiService.GetCurrentTokenFromUI();
            }

            string currentText = _uiService.GetCurrentTokenFromUI();
            if (!string.IsNullOrEmpty(currentText) && !currentText.All(c => c == '●'))
            {
                _realToken = currentText;
            }
            return _realToken;
        }

        public bool ValidateSettings()
        {
            string token = GetCurrentToken();
            string users = _uiService.GetAuthorizedUsersFromUI();

            if (string.IsNullOrEmpty(token))
            {
                _uiService.ShowValidationError(Loc.Instance["Validation_EmptyToken"]);
                return false;
            }

            if (string.IsNullOrEmpty(users))
            {
                _uiService.ShowValidationError(Loc.Instance["Validation_EmptyUsers"]);
                return false;
            }

            return true;
        }

        public void HandleAutoStartToggle()
        {
            try
            {
                var settings = SettingsManager.Current;
                bool currentState = settings.AutoStartEnabled;
                
                AutoStartManager.SetAutoStart(!currentState);
                _uiService.UpdateAutoStartStatus();
                
                LogRequested?.Invoke(string.Format(Loc.Instance["Window_CurrentPath"], AutoStartManager.GetDetectedPath()));
                LogRequested?.Invoke(!currentState ? Loc.Instance["Log_AutoStartEnabled"] : Loc.Instance["Log_AutoStartDisabled"]);
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_AutoStartChange"], ex.Message));
                _uiService.ShowValidationError(ex.Message);
            }
        }
    }
}
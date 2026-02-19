using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot.Services
{
    public class UIService
    {
        private const int MAX_LOG_LINES = 1000;

        private readonly MainWindow _window;

        public UIService(MainWindow window)
        {
            _window = window;
        }

        public void UpdateStatus(bool isRunning)
        {
            _window.Dispatcher.Invoke(() =>
            {
                string statusText = isRunning 
                    ? Loc.Instance["Status_Running"]
                    : Loc.Instance["Status_Stopped"];
                    
                _window.StatusLabel.Text = statusText;
                _window.StatusLabel.Foreground = new SolidColorBrush(isRunning ? Colors.Green : Colors.Red);

                var statusBorder = (Border)_window.StatusLabel.Parent;
                statusBorder.Background = new SolidColorBrush(isRunning
                    ? System.Windows.Media.Color.FromRgb(232, 245, 233)
                    : System.Windows.Media.Color.FromRgb(248, 215, 218));
                statusBorder.BorderBrush = new SolidColorBrush(isRunning
                    ? System.Windows.Media.Color.FromRgb(195, 230, 203)
                    : System.Windows.Media.Color.FromRgb(245, 198, 203));

                UpdateButtonStates(isRunning);
            });
        }

        public void UpdateButtonStates(bool isRunning)
        {
            _window.StartButton.IsEnabled = !isRunning;
            _window.StopButton.IsEnabled = isRunning;
            _window.RestartButton.IsEnabled = true;
        }

        public void InitializeLanguageSelector()
        {
            try
            {
                var settings = SettingsManager.Current;
                string currentLanguage = string.IsNullOrEmpty(settings.Language) ? "en-US" : settings.Language;

                // Set the selected language in the combo box
                foreach (ComboBoxItem item in _window.LanguageComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentLanguage)
                    {
                        _window.LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }

                LogMessage(string.Format(Loc.Instance["Log_LocalizationInitialized"]));
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(Loc.Instance["Error_LanguageSelector"], ex.Message));
            }
        }

        public void UpdateUIAfterLanguageChange(bool isTokenVisible, string realToken, bool isStarting, bool isStopping, bool isRestarting)
        {
            try
            {
                // Updating the tooltip for the show token button
                _window.ShowTokenButton.ToolTip = isTokenVisible
                    ? Loc.Instance["Settings_HideToken"]
                    : Loc.Instance["Settings_ShowToken"];

                // We update the texts of the buttons if they are in the process of execution
                if (isStarting && !_window.StartButton.IsEnabled)
                {
                    _window.StartButton.Content = Loc.Instance["Controls_Starting"];
                }
                if (isStopping && !_window.StopButton.IsEnabled)
                {
                    _window.StopButton.Content = Loc.Instance["Controls_Stopping"];
                }
                if (isRestarting && !_window.RestartButton.IsEnabled)
                {
                    _window.RestartButton.Content = Loc.Instance["Controls_Restarting"];
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(Loc.Instance["Error_UIUpdate"], ex.Message));
            }
        }

        public void UpdateAutoStartStatus()
        {
            try
            {
                var settings = SettingsManager.Current;
                bool isEnabled = settings.AutoStartEnabled;

                // Update the state of the switch
                _window.AutoStartToggle.IsChecked = isEnabled;

                // Updating the label text
                string labelText = isEnabled
                    ? Loc.Instance["AutoStart_Enabled"]
                    : Loc.Instance["AutoStart_Disabled"];

                _window.AutoStartLabel.Text = labelText;
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(Loc.Instance["Error_AutoStartStatus"], ex.Message));
            }
        }

        public void SetTokenVisibility(bool isVisible, string realToken)
        {
            try
            {
                _window.TokenTextBox.Text = isVisible ? realToken : new string('●', realToken.Length);
                _window.ShowTokenButton.Content = isVisible ? "🙈" : "👁";
                _window.ShowTokenButton.ToolTip = isVisible
                    ? Loc.Instance["Settings_HideToken"]
                    : Loc.Instance["Settings_ShowToken"];
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(Loc.Instance["Error_TokenToggle"], ex.Message));
            }
        }

        public void LoadUISettings(string realToken, string authorizedUsers)
        {
            if (!string.IsNullOrEmpty(realToken))
            {
                _window.TokenTextBox.Text = new string('●', realToken.Length);
            }

            if (!string.IsNullOrEmpty(authorizedUsers))
            {
                _window.UsersTextBox.Text = authorizedUsers;
            }
        }

        public string GetCurrentTokenFromUI()
        {
            return _window.TokenTextBox.Text?.Trim() ?? string.Empty;
        }

        public string GetAuthorizedUsersFromUI()
        {
            return _window.UsersTextBox.Text?.Trim() ?? string.Empty;
        }

        public void SetButtonContent(System.Windows.Controls.Button button, string content)
        {
            button.Content = content;
        }

        public void SetButtonEnabled(System.Windows.Controls.Button button, bool enabled)
        {
            button.IsEnabled = enabled;
        }

        public void ClearLog()
        {
            _window.LogTextBox.Clear();
        }

        public void ShowValidationError(string message)
        {
            System.Windows.MessageBox.Show(
                message,
                Loc.Instance["Dialog_Error"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void LogMessage(string message)
        {
            _window.Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _window.LogTextBox.AppendText($"[{timestamp}] {message}\r\n");

                if (_window.LogTextBox.LineCount > MAX_LOG_LINES + 50)
                {
                    var lines = _window.LogTextBox.Text.Split('\n');
                    _window.LogTextBox.Text = string.Join('\n', lines.Skip(lines.Length - MAX_LOG_LINES));
                }

                _window.LogTextBox.ScrollToEnd();
            });
        }
    }
}
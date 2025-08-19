using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms; // Alias for WinForms
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot
{
    public partial class MainWindow : Window
    {
        private readonly BotService _botService;
        private Forms.NotifyIcon? _notifyIcon;
        private Forms.ContextMenuStrip? _trayMenu;
        private bool _isTokenVisible;
        private string _realToken = string.Empty;
        private bool _firstMinimize = true;
        private bool _isAutoStartMode;
        private bool _isExiting = false; // Flag to prevent repeated calls during exit

        public MainWindow()
        {
            string[] args = Environment.GetCommandLineArgs();
            _isAutoStartMode = args.Length > 1 && args[1] == "-autostart";

            InitializeComponent();
            
            Loc.Instance.InitializeDefaultLanguage();
            _botService = new BotService();

            InitializeTrayIcon();
            LoadSettings();
            SubscribeToEvents();

            
            InitializeLanguageSelector();

            // If this is autostart - immediately hide to tray and don't show window
            if (_isAutoStartMode)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                Hide();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                }
            }

            UpdateButtonStates();
            UpdateAutoStartStatus();
            _ = TryAutoStartBot();
        }

        private void InitializeLanguageSelector()
        {
            try
            {
                var settings = SettingsManager.Current;
                string currentLanguage = string.IsNullOrEmpty(settings.Language) ? "en-US" : settings.Language;

                // Set the selected language in the combo box
                foreach (ComboBoxItem item in LanguageComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentLanguage)
                    {
                        LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }

                AddLog(string.Format(Loc.Instance["Log_LocalizationInitialized"]));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_LanguageSelector"], ex.Message));
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                {
                    string languageCode = selectedItem.Tag.ToString();
                    Loc.Instance.ChangeCulture(languageCode);

                    // Updating UI elements that are not tied to localization
                    UpdateUIAfterLanguageChange();

                    AddLog(string.Format(Loc.Instance["Log_LanguageChanged"]));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_LanguageChange"], ex.Message));
            }
        }

        private void UpdateUIAfterLanguageChange()
        {
            try
            {
                // Updating the tooltip for the show token button
                ShowTokenButton.ToolTip = _isTokenVisible
                    ? Loc.Instance["Settings_HideToken"]
                    : Loc.Instance["Settings_ShowToken"];

                // Updating the autostart status
                UpdateAutoStartStatus();

                // Updating the bot status
                UpdateStatus(_botService.IsRunning);

                // We update the texts of the buttons if they are in the process of execution
                if (!StartButton.IsEnabled && StartButton.Content.ToString().Contains("..."))
                {
                    StartButton.Content = Loc.Instance["Controls_Starting"];
                }
                if (!StopButton.IsEnabled && StopButton.Content.ToString().Contains("..."))
                {
                    StopButton.Content = Loc.Instance["Controls_Stopping"];
                }
                if (!RestartButton.IsEnabled && RestartButton.Content.ToString().Contains("..."))
                {
                    RestartButton.Content = Loc.Instance["Controls_Restarting"];
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_UIUpdate"], ex.Message));
            }
        }


        private void InitializeTrayIcon()
        {
            try
            {
                _trayMenu = new Forms.ContextMenuStrip
                {
                    Items =
                    {
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Show"], null, (_, _) => ShowMainWindow()),
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Hide"], null, (_, _) => HideToTray()),
                        new Forms.ToolStripSeparator(),
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Exit"], null, async (_, _) => await ExitApplicationAsync())
                    }
                };

                _notifyIcon = new Forms.NotifyIcon
                {
                    Icon = GetTrayIcon() ?? SystemIcons.Application,
                    Text = "WinControlBot",
                    Visible = false,
                    ContextMenuStrip = _trayMenu
                };

                _notifyIcon.DoubleClick += (_, _) => ToggleWindowVisibility();
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_TrayInit"], ex.Message));
            }
        }

        private Icon? GetTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "favicon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TelegramSystemBot.Resources.favicon.ico");
                if (iconStream != null)
                {
                    return new Icon(iconStream);
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_IconLoad"], ex.Message));
            }

            return null;
        }

        private void UpdateTrayIcon()
        {
            if (_notifyIcon == null) return;

            try
            {
                _notifyIcon.Text = $"WinControlBot - {(_botService.IsRunning ? Loc.Instance["TrayMenu_Running"] : Loc.Instance["TrayMenu_Stopped"])}";
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_TrayUpdate"], ex.Message));
            }
        }

        private void ToggleWindowVisibility()
        {
            if (WindowState == WindowState.Minimized || !IsVisible)
            {
                ShowMainWindow();
            }
            else
            {
                HideToTray();
            }
        }

        private void ShowMainWindow()
        {
            try
            {
                Show();
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
                Activate();
                Focus();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_ShowWindow"], ex.Message));
            }
        }

        private void HideToTray()
        {
            try
            {
                Hide();
                ShowInTaskbar = false;
                if (_notifyIcon == null) return;

                _notifyIcon.Visible = true;

                // Show notification only if this is not autostart
                if (_firstMinimize && !_isAutoStartMode)
                {
                    _firstMinimize = false;
                    _notifyIcon.ShowBalloonTip(3000, "WinControlBot", Loc.Instance["Notification_ContinuesInTray"],
                        Forms.ToolTipIcon.Info);
                }
                else if (_firstMinimize)
                {
                    _firstMinimize = false; // Just mark that first minimization happened
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_HideWindow"], ex.Message));
            }
        }

        private async Task ExitApplicationAsync()
        {
            if (_isExiting) return; // Prevent repeated calls
            _isExiting = true;

            try
            {
                AddLog(string.Format(Loc.Instance["Log_ApplicationExit"]));

                if (_botService.IsRunning)
                {
                    AddLog(string.Format(Loc.Instance["Log_StoppingBot"]));

                    // Use ConfigureAwait(false) to avoid deadlock
                    await _botService.StopAsync().ConfigureAwait(false);

                    AddLog(string.Format(Loc.Instance["Log_BotStopped"]));
                }
                
                // Clean up tray in UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    CleanupTrayIcon();
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_Exit"], ex.Message));
                // Force close application even on error
                await Dispatcher.InvokeAsync(() =>
                {
                    CleanupTrayIcon();
                    System.Windows.Application.Current.Shutdown();
                });
            }
        }

        private void CleanupTrayIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                if (_trayMenu != null)
                {
                    _trayMenu.Dispose();
                    _trayMenu = null;
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_TrayCleanup"], ex.Message));
            }
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_isExiting)
                {
                    // If already exiting, just continue
                    base.OnClosing(e);
                    return;
                }

                if (_botService.IsRunning)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }

                CleanupTrayIcon();
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_Closing"], ex.Message));
            }
            
            base.OnClosing(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Minimized)
                {
                    HideToTray();
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_WindowState"], ex.Message));
            }
            base.OnStateChanged(e);
        }

        private void LoadSettings()
        {
            try
            {
                var settings = SettingsManager.Current;
                if (!string.IsNullOrEmpty(settings.BotToken))
                {
                    _realToken = settings.BotToken;
                    _botService.Token = settings.BotToken;
                    TokenTextBox.Text = new string('●', settings.BotToken.Length);
                }

                if (!string.IsNullOrEmpty(settings.AuthorizedUsers))
                {
                    UsersTextBox.Text = settings.AuthorizedUsers;
                    _botService.AuthorizedUsers = settings.AuthorizedUsers
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => long.TryParse(x.Trim(), out var id) ? id : 0)
                        .Where(x => x > 0)
                        .ToArray();
                }

                // Synchronize autostart state
                AutoStartManager.SynchronizeState();

                AddLog(string.Format(Loc.Instance["Window_SettingsLoaded"]));
                AddLog(string.Format(Loc.Instance["Window_SettingsPath"], SettingsManager.GetSettingsFilePath()));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_SettingsLoad"], ex.Message));
            }
        }

        private void SaveSettings()
        {
            try
            {
                string token = GetCurrentToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    AddLog(string.Format(Loc.Instance["Validation_EmptyTokenWarning"]));
                    return;
                }

                var settings = SettingsManager.Current;
                settings.BotToken = token;
                settings.AuthorizedUsers = UsersTextBox.Text?.Trim() ?? string.Empty;
                SettingsManager.Save();

                _botService.Token = token;
                _realToken = token;
                _botService.AuthorizedUsers = (UsersTextBox.Text ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => long.TryParse(x.Trim(), out var id) ? id : 0)
                    .Where(x => x > 0)
                    .ToArray();

                AddLog(string.Format(Loc.Instance["Window_SettingsSaved"]));
                AddLog(string.Format(Loc.Instance["Window_TokenSaved"], token[..Math.Min(10, token.Length)]));
                AddLog(string.Format(Loc.Instance["Window_UserCount"], _botService.AuthorizedUsers.Length));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_SettingsSave"], ex.Message));
            }
        }

        private string GetCurrentToken()
        {
            if (_isTokenVisible)
            {
                return TokenTextBox.Text?.Trim() ?? string.Empty;
            }

            string currentText = TokenTextBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(currentText) && !currentText.All(c => c == '●'))
            {
                _realToken = currentText;
            }
            return _realToken;
        }

        private void SubscribeToEvents()
        {
            _botService.LogReceived += AddLog;
            _botService.StatusChanged += UpdateStatusAndTray;
        }

        private void UpdateStatusAndTray(bool isRunning)
        {
            UpdateStatus(isRunning);
            UpdateTrayIcon();
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\r\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void UpdateStatus(bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                string statusText = isRunning 
                    ? Loc.Instance["Status_Running"]
                    : Loc.Instance["Status_Stopped"];
                    
                StatusLabel.Text = statusText;
                StatusLabel.Foreground = new SolidColorBrush(isRunning ? Colors.Green : Colors.Red);

                var statusBorder = (Border)StatusLabel.Parent;
                statusBorder.Background = new SolidColorBrush(isRunning
                    ? System.Windows.Media.Color.FromRgb(232, 245, 233)
                    : System.Windows.Media.Color.FromRgb(248, 215, 218));
                statusBorder.BorderBrush = new SolidColorBrush(isRunning
                    ? System.Windows.Media.Color.FromRgb(195, 230, 203)
                    : System.Windows.Media.Color.FromRgb(245, 198, 203));

                UpdateButtonStates();
            });
        }

        private void UpdateButtonStates()
        {
            var isRunning = _botService.IsRunning;
            StartButton.IsEnabled = !isRunning;
            StopButton.IsEnabled = isRunning;
            RestartButton.IsEnabled = true;
        }

        private void ShowTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isTokenVisible = !_isTokenVisible;
                TokenTextBox.Text = _isTokenVisible ? _realToken : new string('●', _realToken.Length);
                ShowTokenButton.Content = _isTokenVisible ? "🔓" : "👁";
                ShowTokenButton.ToolTip = _isTokenVisible
                    ? Loc.Instance["Settings_HideToken"]
                    : Loc.Instance["Settings_ShowToken"];
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_TokenToggle"], ex.Message));
            }
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            if (string.IsNullOrEmpty(_botService.Token) || _botService.AuthorizedUsers.Length == 0)
            {
                System.Windows.MessageBox.Show(
                    string.IsNullOrEmpty(_botService.Token)
                        ? Loc.Instance["Validation_EmptyToken"]
                        : Loc.Instance["Validation_EmptyUsers"],
                    Loc.Instance["Dialog_Error"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            StartButton.IsEnabled = false;
            StartButton.Content = Loc.Instance["Button_Starting"];
            try
            {
                await _botService.StartAsync();
            }
            finally
            {
                StartButton.Content = Loc.Instance["Button_Start"];
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
            StopButton.Content = Loc.Instance["Button_Stopping"];
            try
            {
                await _botService.StopAsync();
            }
            finally
            {
                StopButton.Content = Loc.Instance["Button_Stop"];
            }
        }

        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            RestartButton.IsEnabled = false;
            RestartButton.Content = Loc.Instance["Button_Restarting"];
            try
            {
                if (_botService.IsRunning)
                {
                    await _botService.StopAsync();
                    await Task.Delay(1000);
                }

                SaveSettings();
                if (string.IsNullOrEmpty(_botService.Token) || _botService.AuthorizedUsers.Length == 0)
                {
                    System.Windows.MessageBox.Show(
                        string.IsNullOrEmpty(_botService.Token)
                            ? Loc.Instance["Validation_EmptyToken"]
                            : Loc.Instance["Validation_EmptyUsers"],
                        Loc.Instance["Dialog_Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await _botService.StartAsync();
            }
            finally
            {
                RestartButton.Content = Loc.Instance["Button_Restart"];
                RestartButton.IsEnabled = true;
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            AddLog(Loc.Instance["Log_Cleared"]);
        }

        private async Task TryAutoStartBot()
        {
            if (!string.IsNullOrEmpty(_botService.Token) && _botService.AuthorizedUsers.Length > 0)
            {
                AddLog(Loc.Instance["Log_AutoStarting"]);
                try
                {
                    await _botService.StartAsync();
                }
                catch (Exception ex)
                {
                    AddLog(string.Format(Loc.Instance["Error_AutoStart"], ex.Message));
                }
            }
            else
            {
                AddLog(Loc.Instance["Window_AutoStartSkipped"]);
            }
        }
        
        private void AutoStartToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.Current;
                bool currentState = settings.AutoStartEnabled;
                
                AutoStartManager.SetAutoStart(!currentState);
                UpdateAutoStartStatus();
                
                AddLog(string.Format(Loc.Instance["Window_CurrentPath"], AutoStartManager.GetDetectedPath()));
                AddLog(!currentState ? Loc.Instance["Log_AutoStartEnabled"] : Loc.Instance["Log_AutoStartDisabled"]);
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_AutoStartChange"], ex.Message));
                System.Windows.MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateAutoStartStatus()
        {
            try
            {
                var settings = SettingsManager.Current;
                bool isEnabled = settings.AutoStartEnabled;

                // Update the state of the switch
                AutoStartToggle.IsChecked = isEnabled;

                // Updating the label text
                string labelText = isEnabled
                    ? Loc.Instance["AutoStart_Enabled"]
                    : Loc.Instance["AutoStart_Disabled"];

                AutoStartLabel.Text = labelText;
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_AutoStartStatus"], ex.Message));
            }
        }


        private void AutoStartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.Current;
                bool currentState = settings.AutoStartEnabled;

                AutoStartManager.SetAutoStart(!currentState);
                UpdateAutoStartStatus();

                AddLog(string.Format(Loc.Instance["Window_CurrentPath"], AutoStartManager.GetDetectedPath()));
                AddLog(!currentState ? Loc.Instance["Log_AutoStartEnabled"] : Loc.Instance["Log_AutoStartDisabled"]);
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_AutoStartStatus"], ex.Message));
                System.Windows.MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
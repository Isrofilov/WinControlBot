using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinControlBot.Services;
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot
{
    public partial class MainWindow : Window
    {
        private readonly BotService _botService;
        private readonly TrayIconService _trayIconService;
        private readonly WindowService _windowService;
        private readonly UIService _uiService;
        private readonly SettingsService _settingsService;
        
        private const int MAX_LOG_LINES = 1000;

        private bool _isExiting = false;
        private bool _isAutoStartMode;

        // States for UI updates after language change
        private bool _isStarting = false;
        private bool _isStopping = false;
        private bool _isRestarting = false;

        public MainWindow()
        {
            string[] args = Environment.GetCommandLineArgs();
            _isAutoStartMode = args.Length > 1 && args[1] == "-autostart";

            InitializeComponent();
            
            // Initialize localization
            Loc.Instance.InitializeDefaultLanguage();
            
            // Initialize services
            _botService = new BotService();
            _trayIconService = new TrayIconService(_isAutoStartMode);
            _windowService = new WindowService(this, _trayIconService);
            _uiService = new UIService(this);
            _settingsService = new SettingsService(_uiService, _botService);

            // Setup event handlers
            SubscribeToEvents();
            
            // Initialize UI components
            _uiService.InitializeLanguageSelector();
            _settingsService.LoadSettings();
            _uiService.UpdateAutoStartStatus();
            
            // Setup window state
            _windowService.InitializeWindowState(_isAutoStartMode);
            
            // Update UI state
            _uiService.UpdateStatus(_botService.IsRunning);

            // Try auto-start if conditions are met
            _ = TryAutoStartBot();
        }

        private void SubscribeToEvents()
        {
            // Bot service events
            _botService.LogReceived += AddLog;
            _botService.StatusChanged += OnBotStatusChanged;

            // Tray service events
            _trayIconService.ShowMainWindowRequested += _windowService.ShowMainWindow;
            _trayIconService.HideToTrayRequested += _windowService.HideToTray;
            _trayIconService.ExitApplicationRequested += ExitApplicationAsync;
            _trayIconService.LogRequested += AddLog;

            // Window service events
            _windowService.LogRequested += AddLog;

            // Settings service events
            _settingsService.LogRequested += AddLog;
        }

        private void OnBotStatusChanged(bool isRunning)
        {
            _uiService.UpdateStatus(isRunning);
            _trayIconService.UpdateTrayIcon(isRunning);
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\r\n");

                if (LogTextBox.LineCount > MAX_LOG_LINES + 50)
                {
                    var lines = LogTextBox.Text.Split('\n');
                    LogTextBox.Text = string.Join('\n', lines.Skip(lines.Length - MAX_LOG_LINES));
                }

                LogTextBox.ScrollToEnd();
            });
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                {
                    string languageCode = selectedItem.Tag.ToString();
                    Loc.Instance.ChangeCulture(languageCode);

                    _uiService.UpdateUIAfterLanguageChange(
                        _settingsService.IsTokenVisible, 
                        _settingsService.RealToken,
                        _isStarting, 
                        _isStopping, 
                        _isRestarting);
                    
                    _uiService.UpdateAutoStartStatus();
                    _uiService.UpdateStatus(_botService.IsRunning);

                    AddLog(string.Format(Loc.Instance["Log_LanguageChanged"]));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_LanguageChange"], ex.Message));
            }
        }

        private void ShowTokenButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.ToggleTokenVisibility();
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.SaveSettings();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.SaveSettings();
            
            if (!_settingsService.ValidateSettings())
                return;

            _isStarting = true;
            _uiService.SetButtonEnabled(StartButton, false);
            _uiService.SetButtonContent(StartButton, Loc.Instance["Button_Starting"]);
            
            try
            {
                await _botService.StartAsync();
            }
            finally
            {
                _uiService.SetButtonContent(StartButton, Loc.Instance["Button_Start"]);
                _isStarting = false;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _isStopping = true;
            _uiService.SetButtonEnabled(StopButton, false);
            _uiService.SetButtonContent(StopButton, Loc.Instance["Button_Stopping"]);
            
            try
            {
                await _botService.StopAsync();
            }
            finally
            {
                _uiService.SetButtonContent(StopButton, Loc.Instance["Button_Stop"]);
                _isStopping = false;
            }
        }

        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            _isRestarting = true;
            _uiService.SetButtonEnabled(RestartButton, false);
            _uiService.SetButtonContent(RestartButton, Loc.Instance["Button_Restarting"]);
            
            try
            {
                if (_botService.IsRunning)
                {
                    await _botService.StopAsync();
                    await Task.Delay(1000);
                }

                _settingsService.SaveSettings();
                
                if (!_settingsService.ValidateSettings())
                    return;

                await _botService.StartAsync();
            }
            finally
            {
                _uiService.SetButtonContent(RestartButton, Loc.Instance["Button_Restart"]);
                _uiService.SetButtonEnabled(RestartButton, true);
                _isRestarting = false;
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            _uiService.ClearLog();
            AddLog(Loc.Instance["Log_Cleared"]);
        }

        private void AutoStartToggle_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.HandleAutoStartToggle();
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

        private async Task ExitApplicationAsync()
        {
            if (_isExiting) return;
            _isExiting = true;

            try
            {
                AddLog(string.Format(Loc.Instance["Log_ApplicationExit"]));

                if (_botService.IsRunning)
                {
                    AddLog(string.Format(Loc.Instance["Log_StoppingBot"]));
                    await _botService.StopAsync().ConfigureAwait(false);
                    AddLog(string.Format(Loc.Instance["Log_BotStopped"]));
                }
                
                await Dispatcher.InvokeAsync(() =>
                {
                    _trayIconService.Dispose();
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_Exit"], ex.Message));
                await Dispatcher.InvokeAsync(() =>
                {
                    _trayIconService.Dispose();
                    System.Windows.Application.Current.Shutdown();
                });
            }
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_isExiting)
                {
                    base.OnClosing(e);
                    return;
                }

                if (_botService.IsRunning)
                {
                    e.Cancel = true;
                    _windowService.HideToTray();
                    return;
                }

                _trayIconService.Dispose();
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
                    _windowService.HideToTray();
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(Loc.Instance["Error_WindowState"], ex.Message));
            }
            base.OnStateChanged(e);
        }
    }
}
using System;
using System.Windows;
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot.Services
{
    public class WindowService
    {
        private readonly MainWindow _window;
        private readonly TrayIconService _trayIconService;

        public event Action<string>? LogRequested;

        public WindowService(MainWindow window, TrayIconService trayIconService)
        {
            _window = window;
            _trayIconService = trayIconService;
        }

        public void ShowMainWindow()
        {
            try
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.ShowInTaskbar = true;
                _window.Activate();
                _window.Focus();
                _trayIconService.ShowTrayIcon();
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_ShowWindow"], ex.Message));
            }
        }

        public void HideToTray()
        {
            try
            {
                _window.Hide();
                _window.ShowInTaskbar = false;
                _trayIconService.ShowTrayIcon();
                _trayIconService.ShowNotification();
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_HideWindow"], ex.Message));
            }
        }

        public void ToggleWindowVisibility()
        {
            if (_window.WindowState == WindowState.Minimized || !_window.IsVisible)
            {
                ShowMainWindow();
            }
            else
            {
                HideToTray();
            }
        }

        public void InitializeWindowState(bool isAutoStartMode)
        {
            if (isAutoStartMode)
            {
                _window.WindowState = WindowState.Minimized;
                _window.ShowInTaskbar = false;
                _window.Hide();
                _trayIconService.ShowTrayIcon();
            }
        }
    }
}
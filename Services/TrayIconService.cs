using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using Loc = WinControlBot.Localization.LocalizationManager;

namespace WinControlBot.Services
{
    public class TrayIconService : IDisposable
    {
        private Forms.NotifyIcon? _notifyIcon;
        private Forms.ContextMenuStrip? _trayMenu;
        private bool _firstMinimize = true;
        private bool _isAutoStartMode;

        public event Action? ShowMainWindowRequested;
        public event Action? HideToTrayRequested;
        public event Func<Task>? ExitApplicationRequested;
        public event Action<string>? LogRequested;

        public TrayIconService(bool isAutoStartMode)
        {
            _isAutoStartMode = isAutoStartMode;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon(bool isRunning = false)
        {
            try
            {
                _trayMenu = new Forms.ContextMenuStrip
                {
                    Items =
                    {
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Show"], null, (_, _) => ShowMainWindowRequested?.Invoke()),
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Hide"], null, (_, _) => HideToTrayRequested?.Invoke()),
                        new Forms.ToolStripSeparator(),
                        new Forms.ToolStripMenuItem(Loc.Instance["TrayMenu_Exit"], null, async (_, _) => 
                        {
                            if (ExitApplicationRequested != null)
                                await ExitApplicationRequested.Invoke();
                        })
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
                UpdateTrayIcon(isRunning);
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_TrayInit"], ex.Message));
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
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_IconLoad"], ex.Message));
            }

            return null;
        }

        public void UpdateTrayIcon(bool isRunning)
        {
            if (_notifyIcon == null) return;

            try
            {
                _notifyIcon.Text = $"WinControlBot - {(isRunning ? Loc.Instance["TrayMenu_Running"] : Loc.Instance["TrayMenu_Stopped"])}";
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_TrayUpdate"], ex.Message));
            }
        }

        public void ShowTrayIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }

        public void ShowNotification()
        {
            if (_notifyIcon == null) return;

            try
            {
                if (_firstMinimize && !_isAutoStartMode)
                {
                    _firstMinimize = false;
                    _notifyIcon.ShowBalloonTip(3000, "WinControlBot", Loc.Instance["Notification_ContinuesInTray"],
                        Forms.ToolTipIcon.Info);
                }
                else if (_firstMinimize)
                {
                    _firstMinimize = false;
                }
            }
            catch (Exception ex)
            {
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_Notification"], ex.Message));
            }
        }

        private void ToggleWindowVisibility()
        {
            // This will be handled by the main window through events
            ShowMainWindowRequested?.Invoke();
        }

        public void Dispose()
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
                LogRequested?.Invoke(string.Format(Loc.Instance["Error_TrayCleanup"], ex.Message));
            }
        }
    }
}
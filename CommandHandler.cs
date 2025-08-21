using WinControlBot.Localization;

namespace WinControlBot
{
    public interface ICommandHandler
    {
        event Action<string>? LogReceived;
        Task HandleCommandAsync(CommandContext context);
    }

    public class CommandHandler : ICommandHandler
    {
        private readonly ISystemService _systemService;
        private readonly IScreenshotService _screenshotService;

        public event Action<string>? LogReceived;

        public CommandHandler(ISystemService? systemService = null, IScreenshotService? screenshotService = null)
        {
            _systemService = systemService ?? new SystemService();
            _screenshotService = screenshotService ?? new ScreenshotService();

            _systemService.LogReceived += (message) => LogReceived?.Invoke(message);
            _screenshotService.LogReceived += (message) => LogReceived?.Invoke(message);
        }

        public async Task HandleCommandAsync(CommandContext context)
        {
            var command = context.Message.Text.ToLowerInvariant();

            switch (command)
            {
                case "/start":
                    await SendKeyboardMessageAsync(context);
                    break;
                default:
                    await HandleButtonCommandAsync(context);
                    break;
            }
        }

        private async Task HandleButtonCommandAsync(CommandContext context)
        {
            var buttonText = context.Message.Text;

            if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Status"])
            {
                await HandleStatusCommandAsync(context);
            }
            else if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Screenshot"])
            {
                await HandleScreenshotCommandAsync(context);
            }
            else if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Sleep"])
            {
                await HandleAuthorizedCommandAsync(context, "Bot_Sleep", "rundll32.exe powrprof.dll,SetSuspendState 0,1,0");
            }
            else if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Hibernate"])
            {
                await HandleAuthorizedCommandAsync(context, "Bot_Hibernate", "rundll32.exe powrprof.dll,SetSuspendState Hibernate");
            }
            else if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Shutdown"])
            {
                await HandleAuthorizedCommandAsync(context, "Bot_Shutdown", "shutdown /s /t 0");
            }
            else if (buttonText == LocalizationManager.Instance["Bot_Keyboard_Restart"])
            {
                await HandleAuthorizedCommandAsync(context, "Bot_Restart", "shutdown /r /t 0");
            }
            else
            {
                // Unknown command - show keyboard
                await SendKeyboardMessageAsync(context);
            }
        }

        private async Task SendKeyboardMessageAsync(CommandContext context)
        {
            var keyboard = new
            {
                keyboard = new[]
                {
                    new[] { LocalizationManager.Instance["Bot_Keyboard_Status"], LocalizationManager.Instance["Bot_Keyboard_Screenshot"] },
                    new[] { LocalizationManager.Instance["Bot_Keyboard_Sleep"], LocalizationManager.Instance["Bot_Keyboard_Hibernate"] },
                    new[] { LocalizationManager.Instance["Bot_Keyboard_Shutdown"], LocalizationManager.Instance["Bot_Keyboard_Restart"] }
                },
                resize_keyboard = true,
                one_time_keyboard = false
            };

            await context.TelegramClient.SendMessageWithKeyboardAsync(
                context.Message.ChatId,
                LocalizationManager.Instance["Bot_Welcome_Message"],
                keyboard);
        }

        private async Task HandleAuthorizedCommandAsync(CommandContext context, string messageKey, string? systemCommand = null)
        {
            if (!context.IsUserAuthorized)
            {
                await SendUnauthorizedMessageAsync(context);
                return;
            }

            await context.TelegramClient.SendMessageAsync(context.Message.ChatId, LocalizationManager.Instance["Bot_Command_Executing"]);
            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ExecutingCommand"], messageKey));

            try
            {
                await context.TelegramClient.SendMessageAsync(context.Message.ChatId, LocalizationManager.Instance[messageKey]);

                if (!string.IsNullOrEmpty(systemCommand))
                {
                    await Task.Delay(2000); // Give user time to see the message
                    await _systemService.ExecuteCommandAsync(systemCommand);
                }
            }
            catch (Exception ex)
            {
                await context.TelegramClient.SendMessageAsync(context.Message.ChatId,
                    string.Format(LocalizationManager.Instance["Bot_Error_Occurred"], ex.Message));
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecutionError"], messageKey, ex.Message));
            }
        }

        private async Task HandleStatusCommandAsync(CommandContext context)
        {
            if (!context.IsUserAuthorized)
            {
                await SendUnauthorizedMessageAsync(context);
                return;
            }

            try
            {
                var status = await _systemService.GetSystemStatusAsync();
                var message = string.Format(LocalizationManager.Instance["Bot_Status"],
                    status.ComputerName,
                    status.ProcessorName,
                    status.Uptime.Days,
                    status.Uptime.Hours,
                    status.Uptime.Minutes,
                    status.Uptime.Seconds,
                    status.UsedRamGB,
                    status.TotalRamGB);

                await context.TelegramClient.SendMessageAsync(context.Message.ChatId, message);
            }
            catch (Exception ex)
            {
                await context.TelegramClient.SendMessageAsync(context.Message.ChatId,
                    string.Format(LocalizationManager.Instance["Bot_Error_Occurred"], ex.Message));
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SystemStatusError"], ex.Message));
            }
        }

        private async Task HandleScreenshotCommandAsync(CommandContext context)
        {
            if (!context.IsUserAuthorized)
            {
                await SendUnauthorizedMessageAsync(context);
                return;
            }

            await context.TelegramClient.SendMessageAsync(context.Message.ChatId, LocalizationManager.Instance["Bot_Command_Executing"]);
            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ExecutingCommand"], "screenshot"));

            try
            {
                var screenshots = await _screenshotService.TakeScreenshotsAsync();

                if (screenshots.Count == 0)
                {
                    await context.TelegramClient.SendMessageAsync(context.Message.ChatId,
                        string.Format(LocalizationManager.Instance["Bot_Screenshot_Error"], "No screens found"));
                    return;
                }

                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ScreenshotsFound"], screenshots.Count));

                if (screenshots.Count == 1)
                {
                    await context.TelegramClient.SendPhotoAsync(context.Message.ChatId, screenshots[0],
                        LocalizationManager.Instance["Bot_Screenshot_Taken"]);
                }
                else
                {
                    // Multiple screens - send with numbering
                    for (int i = 0; i < screenshots.Count; i++)
                    {
                        var caption = string.Format(LocalizationManager.Instance["Bot_Screenshot_Monitor"],
                            i + 1, screenshots.Count);
                        await context.TelegramClient.SendPhotoAsync(context.Message.ChatId, screenshots[i], caption);

                        if (i < screenshots.Count - 1)
                            await Task.Delay(500);
                    }
                }

                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_AllScreenshotsSent"], screenshots.Count));
            }
            catch (Exception ex)
            {
                await context.TelegramClient.SendMessageAsync(context.Message.ChatId,
                    string.Format(LocalizationManager.Instance["Bot_Screenshot_Error"], ex.Message));
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecutionError"], ex.Message));
            }
        }

        private async Task SendUnauthorizedMessageAsync(CommandContext context)
        {
            await context.TelegramClient.SendMessageAsync(context.Message.ChatId,
                string.Format(LocalizationManager.Instance["Bot_Unauthorized"], context.Message.UserId));
            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_UnauthorizedAccess"], context.Message.UserId));
        }
    }

    public class CommandContext
    {
        public TelegramMessage Message { get; set; } = null!;
        public bool IsUserAuthorized { get; set; }
        public ITelegramClient TelegramClient { get; set; } = null!;
    }
}
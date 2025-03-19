# WinControlBot

WinControlBot is a Telegram bot designed to control your Windows system remotely. With a set of easy-to-use Telegram commands, you can put your system to sleep, hibernate it, shut it down, or check the system's uptime. This bot ensures that only authorized users can control the system for security purposes.

## Features

- `/uptime` - Check system uptime.
- `/sleep` - Transition the system into sleep mode.
- `/hibernate` - Hibernate the system.
- `/shutdown` - Shut down the system (with confirmation).
- `/restart` - Restart the system (with confirmation).
- `/sysinfo` - Get system information (CPU, memory, disk usage).
- `/shutdown_in [time]` - Schedule a shutdown after a specified time period.
- `/screenshot` - Take and send a screenshot of your desktop.
- **Automatic request validation** - The bot checks the time difference between the message's sending time and the current system time to prevent the execution of outdated commands (e.g., when the computer was off for an extended period). If the difference exceeds 5 minutes, the bot requests the user to resend the command.
- **Confirmation system** - Critical actions like shutdown and restart require confirmation (30-second delay with cancel option).

## Getting Started

To get started with WinControlBot, follow these steps:

1. Clone the repository to your local machine.
2. Install the required Python packages using `pip install -r requirements.txt`.
3. Obtain your Telegram bot token from [@BotFather](https://t.me/botfather):
   - Start a chat with BotFather.
   - Type `/newbot` and follow the instructions to create a new bot. You will be asked to choose a name and a username for your bot.
   - After the creation process, BotFather will provide you with a token. This is the token you'll use to authenticate your requests.
4. Set your Telegram bot token in the variable `YOUR_TELEGRAM_BOT_TOKEN_HERE`.
5. Add authorized user IDs to the `AUTHORIZED_USERS` list.
6. To run the bot manually, execute `pythonw WinControlBot.pyw`.

## Configure for Automatic Startup on Windows

To have WinControlBot start automatically with Windows, you can add `WinControlBot.pyw` to the Startup folder. Follow these steps:

1. Press `Win + R` to open the Run dialog.
2. Type `shell:startup` and press `Enter` to open the Startup folder.
3. Create a shortcut of `WinControlBot.pyw` by right-clicking on the file and selecting `Create shortcut`. Then, move this shortcut to the Startup folder.

By placing the shortcut in the Startup folder, `WinControlBot.pyw` will be launched automatically whenever you log into Windows. Ensure that the `TOKEN` and `AUTHORIZED_USERS` are correctly set in `WinControlBot.pyw` as this is the file that will be executed.

Now, `WinControlBot.pyw` will run automatically when you log into Windows.

## Commands

- `/start` - Lists all available commands.
- `/uptime` - Sends the current system uptime.
- `/sleep` - Puts the computer into sleep mode.
- `/hibernate` - Hibernates the computer.
- `/shutdown` - Shuts down the computer (with confirmation).
- `/restart` - Restarts the computer (with confirmation).
- `/sysinfo` - Shows system information (CPU, memory, disk usage).
- `/shutdown_in [time]` - Schedules a shutdown after specified time (format: 1h30m, 45m, 10s).
- `/screenshot` - Takes and sends a screenshot of your desktop.

## Time Format for `/shutdown_in`

The `/shutdown_in` command accepts time in the following formats:
- `1h30m` - 1 hour and 30 minutes
- `45m` - 45 minutes
- `10s` - 10 seconds
- Any combination of h (hours), m (minutes), and s (seconds)

## Localization

WinControlBot supports English and Russian languages, automatically responding in the language of the user's Telegram client.

## Security

Make sure to keep your Telegram bot token secure and only add trusted user IDs to the `AUTHORIZED_USERS` list. The bot includes a confirmation system for critical actions like shutdown and restart to prevent accidental triggering.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributions

Contributions are welcome! Please open an issue or pull request if you'd like to help improve WinControlBot.

Happy controlling your Windows system remotely!
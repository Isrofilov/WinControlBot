# WinControlBot
WinControlBot is a powerful Telegram bot that gives you complete remote control over your Windows system. Using simple Telegram commands, you can manage system power states, monitor performance metrics, schedule shutdowns, and even capture screenshots—all from your mobile device or any Telegram client.

## ✨ Key Features

- **System Power Management**
  - `/sleep` - Put your system into sleep mode
  - `/hibernate` - Hibernate your system
  - `/shutdown` - Shut down your system (with confirmation)
  - `/restart` - Restart your system (with confirmation)
  - `/shutdown_in [time]` - Schedule a shutdown with flexible timing

- **System Monitoring**
  - `/uptime` - Check system uptime
  - `/sysinfo` - Get real-time system information (CPU, memory, disk usage)
  - `/screenshot` - Capture and receive your desktop screen

- **Security Features**
  - **Authorized User Protection** - Only predefined users can control the system
  - **Command Validation** - Prevents execution of outdated commands (>5 minutes old)
  - **Confirmation System** - Critical actions require confirmation with 30-second delay and cancel option

- **User Experience**
  - **Multilingual Support** - Automatically responds in English or Russian based on user's Telegram client
  - **Flexible Time Formats** - Intuitive time specifications like "1h30m" or "45m"

## 🚀 Getting Started

### Prerequisites
- Windows operating system
- Python 3.7+
- Telegram account

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/WinControlBot.git
   cd WinControlBot
   ```

2. **Install dependencies**
   ```bash
   pip install -r requirements.txt
   ```

3. **Set up your Telegram bot**
   - Start a chat with [@BotFather](https://t.me/botfather)
   - Send `/newbot` and follow the instructions
   - Copy your bot token when provided

4. **Configure the bot**
   - Open `WinControlBot.pyw` in a text editor
   - Replace `YOUR_TELEGRAM_BOT_TOKEN_HERE` with your actual token
   - Add your Telegram User ID to `AUTHORIZED_USERS` list (you can find your ID by sending a message to [@userinfobot](https://t.me/userinfobot))

5. **Run the bot**
   ```bash
   pythonw WinControlBot.pyw
   ```

## 🔄 Auto-Start Configuration

### Method 1: Windows Startup Folder

1. Press `Win + R` and type `shell:startup`
2. Create a shortcut to `WinControlBot.pyw` in the opened folder

### Method 2: Task Scheduler (Advanced)

1. Open Windows Task Scheduler
2. Create a new task with trigger "At system startup"
3. Set the action to start `pythonw.exe` with argument path to your `WinControlBot.pyw`
4. Configure to run with highest privileges and whether user is logged on or not

## 🤖 Bot Commands

| Command | Description |
|---------|-------------|
| `/start` | Lists all available commands |
| `/uptime` | Shows current system uptime |
| `/sleep` | Puts computer in sleep mode |
| `/hibernate` | Hibernates the computer |
| `/shutdown` | Initiates system shutdown with confirmation |
| `/restart` | Initiates system restart with confirmation |
| `/sysinfo` | Displays CPU, memory and disk usage |
| `/shutdown_in [time]` | Schedules shutdown after specified time |
| `/screenshot` | Captures and sends desktop screenshot |

## ⏱️ Time Format Specification

For the `/shutdown_in` command, use these time formats:
- `1h30m` - 1 hour and 30 minutes
- `45m` - 45 minutes 
- `10s` - 10 seconds
- Any combination of h (hours), m (minutes), and s (seconds)

## 🔒 Security Considerations

- Keep your bot token confidential
- Only add trusted users to the `AUTHORIZED_USERS` list
- Consider the security implications of the screenshot feature
- The bot implements a delay with cancellation option for critical operations

## 🌐 Localization

WinControlBot automatically detects and responds in:
- English (default)
- Russian

The language is automatically selected based on your Telegram client's language setting.

## 📋 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Feel free to:
- Report bugs
- Suggest features
- Submit pull requests

## 📬 Contact

If you have questions or feedback, please open an issue in the repository.

---

**Note**: Using this bot gives remote users control over your computer. Always ensure your Telegram account is secure and only grant access to trusted individuals.

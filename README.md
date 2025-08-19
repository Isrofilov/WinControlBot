# WinControlBot
[🇷🇺 Читать на русском](README.ru.md)

WinControlBot is a powerful Telegram bot that gives you complete remote control over your Windows system. Using simple Telegram commands, you can manage system power states, monitor performance metrics, schedule shutdowns, and even capture screenshots—all from your mobile device or any Telegram client.

## ✨ Key Features

### 🔌 System Power Management

* **`/sleep`** — Put the system into sleep mode
* **`/hibernate`** — Put the system into hibernation mode
* **`/shutdown`** — Shut down the system
* **`/restart`** — Restart the system

### 📊 System Monitoring

* **`/status`** — Get system status (computer name, CPU model, RAM usage, uptime)
* **`/screenshot`** — Take and receive a screenshot of the desktop

### 🛡️ Security

* **Authorized User Protection** — Only pre-approved users can control the system
* **Command Validation** — Prevents execution of outdated commands (older than 5 minutes)

### 🌍 Ease of Use

* **Multilingual Support** — Automatic responses in English or Russian based on your Telegram client settings
* **Flexible Configuration** — Easy setup via the application's graphical interface

## 🚀 Quick Start

### 📋 System Requirements

* **Windows 10** or later
* **Telegram account**
* **Internet connection**

> 💡 **.NET 9 Advantage**: The program is built on .NET 9 with a self-contained runtime—all components are packaged in a single file, no additional installation required!

### 📦 Installation

#### Option 1: Installer (Recommended)

1. Download the latest **installer** from the [releases page](https://github.com/Isrofilov/WinControlBot/releases)
2. Run the installer and follow the instructions
3. The program will automatically be added to the Start menu

#### Option 2: Portable Version

1. Download the **ZIP archive** from the [releases page](https://github.com/Isrofilov/WinControlBot/releases)
2. Extract the archive to a convenient folder
3. Run the executable file

> ⚠️ **Note**: In the portable version, **settings are not saved in the program folder**. Configuration is stored in the system at `%AppData%\WinControlBot`. Settings will not transfer automatically when moving the program to another computer.

### ⚙️ Initial Setup

<<<<<<< HEAD
1. **Create a Telegram Bot**
   * Open a chat with [@BotFather](https://t.me/botfather)
   * Send the `/newbot` command and follow the instructions
   * Save the bot token provided
2. **Configure the Application**
   * Launch WinControlBot
   * Enter the bot token in the designated field
   * Add your Telegram User ID to the list of authorized users
   > 💡 **How to Find Your User ID**: Send a message to [@userinfobot](https://t.me/userinfobot)
3. **Enable Auto-Start** (Optional)
   * In the application interface, toggle the "Auto-Start" option
   * The program will launch automatically on Windows startup

## 🎮 Bot Commands

| Command       | Description                                   | Security Level          |
|---------------|-----------------------------------------------|-------------------------|
| `/start`      | Display a list of all available commands      | ✅ Safe                 |
| `/status`     | Computer name, CPU model, RAM usage, uptime   | ✅ Safe                 |
| `/sleep`      | Put the computer into sleep mode              | ⚠️ Power Management     |
| `/hibernate`  | Put the computer into hibernation mode        | ⚠️ Power Management     |
| `/shutdown`   | Shut down the system                          | ⚠️ Critical Operation   |
| `/restart`    | Restart the system                            | ⚠️ Critical Operation   |
| `/screenshot` | Take and send a screenshot of the desktop     | ⚠️ Privacy              |

## 🔒 Security and Privacy

### 🛡️ Security Measures

* **Keep the Bot Token Secret** — Never share it with third parties
* **Manage Authorized Users** — Only add trusted individuals to the authorized list
* **Be Aware of Screenshot Risks** — The `/screenshot` command may transmit sensitive information

### 🔐 Recommendations

* Use two-factor authentication for your Telegram account
* Regularly review the list of authorized users
* Promptly revoke access for compromised users if needed

## 🌐 Localization

WinControlBot automatically detects and responds in the following languages:

* **English** (default)
* **Russian**

**In the Telegram Bot**: The language is automatically selected based on your Telegram client settings.

**In the Application**: The interface language can be configured in the program settings.

## 🔧 Additional Settings

### ⚡ Auto-Start

Enable auto-start with a simple toggle in the application interface. The program will automatically be added to Windows startup.

## 📋 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

We welcome contributions to the project! You can:

* 🐛 Report bugs
* 💡 Suggest new features
* 🔧 Submit pull requests
* 📚 Improve documentation

### How to Contribute

1. Fork the repository
2. Create a branch for your changes
3. Make changes and add tests
4. Submit a pull request with a detailed description

## 📬 Support

Have questions or suggestions?

* 🐛 **Bugs and Issues**: Create an [issue](https://github.com/Isrofilov/WinControlBot/issues) in the repository

---

## ⚠️ Important Warning

**Using this bot grants remote users control over your computer.**

Ensure you:

* Secure your Telegram account
* Grant access only to trusted individuals
* Regularly review the list of authorized users
* Be mindful of potential risks in corporate environments

---

**⭐ If you find this project useful, please give it a star on GitHub!**

*Built with ❤️ for seamless remote control*

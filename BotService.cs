using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Drawing.Imaging;
using System.Management;
using System.IO;
using System.Runtime.InteropServices;
using WinControlBot.Localization;

namespace WinControlBot
{
    public class BotService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly HttpClient _httpClient;
        private long _offset = 0;
        private readonly SemaphoreSlim _commandSemaphore = new(1, 1);
        private bool _disposed = false;

        private const int TIME_THRESHOLD_SECONDS = 300; // 5 minutes
        private const int REQUEST_TIMEOUT_SECONDS = 30;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 1000;
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB - Telegram limit

        // WinAPI for getting DPI
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        // Constants for GetDeviceCaps
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        public string Token { get; set; } = "";
        public long[] AuthorizedUsers { get; set; } = Array.Empty<long>();
        public bool IsRunning { get; private set; }

        public event Action<string>? LogReceived;
        public event Action<bool>? StatusChanged;

        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["en"] = new()
            {
                ["unauthorized"] = "🚫 You do not have permission to execute this command.\nYour userId is {0}",
                ["commands"] = "🤖 Available commands:\n\n" +
                              "ℹ️ /status - Get system status\n" +
                              "📸 /screenshot - Take a screenshot\n" +
                              "😴 /sleep - Put the system to sleep\n" +
                              "💤 /hibernate - Hibernate the system\n" +
                              "🔴 /shutdown - Shut down the system\n" +
                              "🔄 /restart - Restart the system\n",
                ["sleep"] = "😴 The computer will be put to sleep...",
                ["hibernate"] = "💤 The computer will be hibernated...",
                ["shutdown"] = "🔴 Shutting down the computer...",
                ["restart"] = "🔄 Restarting the computer...",
                ["status"] = "🖥️ System Status:\n" +
                           "💻 Computer: {0}\n" +
                           "⚙️ Processor: {1}\n" +
                           "💾 RAM: {6:F1} GB / {7:F1} GB\n" +
                           "⏱️ Uptime: {2}d {3}h {4}m {5}s",
                ["retry_request"] = "⚠️ Your request was sent too long ago. Please repeat the request.",
                ["command_executing"] = "⏳ Executing command...",
                ["error_occurred"] = "❌ An error occurred: {0}",
                ["screenshot_taken"] = "📸 Screenshot taken",
                ["screenshot_error"] = "❌ Failed to take screenshot: {0}",
                ["file_too_large"] = "❌ Screenshot file is too large to send ({0:F1} MB). Maximum size: {1} MB",
                ["screenshot_monitor"] = "📸 Screenshot {0} of {1}"
            },
            ["ru"] = new()
            {
                ["unauthorized"] = "🚫 У вас нет прав для выполнения этой команды.\nВаш userId {0}",
                ["commands"] = "🤖 Доступные команды:\n\n" +
                              "ℹ️ /status - Получить статус системы\n" +
                              "📸 /screenshot - Сделать скриншот\n" +
                              "😴 /sleep - Перевести в режим сна\n" +
                              "💤 /hibernate - Перевести в режим гибернации\n" +
                              "🔴 /shutdown - Выключить компьютер\n" +
                              "🔄 /restart - Перезагрузить компьютер\n",
                ["sleep"] = "😴 Компьютер будет отправлен в режим сна...",
                ["hibernate"] = "💤 Компьютер будет отправлен в режим гибернации...",
                ["shutdown"] = "🔴 Выключаю компьютер...",
                ["restart"] = "🔄 Перезагружаю компьютер...",
                ["status"] = "🖥️ Статус системы:\n" +
                           "💻 Компьютер: {0}\n" +
                           "⚙️ Процессор: {1}\n" +
                           "💾 ОЗУ: {6:F1} ГБ / {7:F1} ГБ\n" +
                           "⏱️ Время работы: {2}д {3}ч {4}м {5}с" ,
                ["retry_request"] = "⚠️ Ваш запрос был отправлен слишком давно. Пожалуйста, повторите запрос.",
                ["command_executing"] = "⏳ Выполняю команду...",
                ["error_occurred"] = "❌ Произошла ошибка: {0}",
                ["screenshot_taken"] = "📸 Скриншот сделан",
                ["screenshot_error"] = "❌ Не удалось сделать скриншот: {0}",
                ["file_too_large"] = "❌ Файл скриншота слишком большой для отправки ({0:F1} МБ). Максимальный размер: {1} МБ",
                ["screenshot_monitor"] = "📸 Скриншот {0} из {1}"
            }
        };

        public BotService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS + 10)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinControlBot/2");
        }

        public async Task<bool> StartAsync()
        {
            if (IsRunning || string.IsNullOrEmpty(Token))
                return false;

            // Checking the validity of the token
            if (!await ValidateTokenAsync())
            {
                LogReceived?.Invoke(LocalizationManager.Instance["Bot_InvalidToken"]);
                return false;
            }

            _cts = new CancellationTokenSource();
            IsRunning = true;
            StatusChanged?.Invoke(true);
            LogReceived?.Invoke(LocalizationManager.Instance["Log_BotStarted"]);

            try
            {
                await PollUpdatesAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                LogReceived?.Invoke(LocalizationManager.Instance["Bot_PollingCancelled"]);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CriticalError"], ex.Message));
                await StopAsync();
                return false;
            }

            return true;
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            try
            {
                _cts?.Cancel();
                await Task.Delay(100); // Allow time for current operations to complete
                _cts?.Dispose();
                _cts = null;
                IsRunning = false;
                StatusChanged?.Invoke(false);
                LogReceived?.Invoke(LocalizationManager.Instance["Log_BotStopped"]);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Error_BotStop"], ex.Message));
            }
        }

        private async Task<bool> ValidateTokenAsync()
        {
            try
            {
                var url = $"https://api.telegram.org/bot{Token}/getMe";
                using var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    LogReceived?.Invoke($"❌ HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var data = JsonDocument.Parse(json);
                
                return data.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_TokenValidationError"], ex.Message));
                return false;
            }
        }

        private async Task PollUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{Token}/getUpdates?offset={_offset}&timeout={REQUEST_TIMEOUT_SECONDS}&limit=100";
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Conflict)
                        {
                            LogReceived?.Invoke(LocalizationManager.Instance["Bot_Conflict"]);
                            await StopAsync();
                            return;
                        }

                        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                    }

                    var json = await response.Content.ReadAsStringAsync(token);
                    using var data = JsonDocument.Parse(json);

                    if (!data.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                    {
                        throw new Exception("Telegram API returned non-ok response");
                    }

                    if (data.RootElement.TryGetProperty("result", out var resultArray))
                    {
                        foreach (var update in resultArray.EnumerateArray())
                        {
                            if (update.TryGetProperty("update_id", out var updateId))
                            {
                                _offset = updateId.GetInt64() + 1;
                            }

                            if (update.TryGetProperty("message", out var message))
                            {
                                await ProcessMessageAsync(message);
                            }
                        }
                    }
                }
                catch (TaskCanceledException) when (token.IsCancellationRequested) { }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_PollError"], ex.Message));
                    await Task.Delay(RETRY_DELAY_MS, token);
                }
            }
        }

        private async Task ProcessMessageAsync(JsonElement message)
        {
            if (!TryExtractMessageData(message, out var chatId, out var userId, out var text, out var languageCode))
                return;

            if (IsMessageTooOld(message))
            {
                await SendReplyAsync(chatId, "retry_request", languageCode);
                return;
            }

            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_MessageReceived"], userId, text));

            await _commandSemaphore.WaitAsync();
            try
            {
                await ExecuteCommandAsync(text, chatId, userId, languageCode);
            }
            finally
            {
                _commandSemaphore.Release();
            }
        }

        private static bool TryExtractMessageData(JsonElement message, out long chatId, out long userId, out string text, out string languageCode)
        {
            chatId = 0;
            userId = 0;
            text = "";
            languageCode = "en";

            if (!message.TryGetProperty("chat", out var chat) ||
                !chat.TryGetProperty("id", out var chatIdElem) ||
                !message.TryGetProperty("from", out var from) ||
                !from.TryGetProperty("id", out var userIdElem) ||
                !message.TryGetProperty("text", out var textElem))
            {
                return false;
            }

            chatId = chatIdElem.GetInt64();
            userId = userIdElem.GetInt64();
            text = textElem.GetString() ?? "";
            languageCode = from.TryGetProperty("language_code", out var lang) ? lang.GetString() ?? "en" : "en";

            return true;
        }

        private static bool IsMessageTooOld(JsonElement message)
        {
            if (message.TryGetProperty("date", out var dateElem))
            {
                var messageTime = DateTimeOffset.FromUnixTimeSeconds(dateElem.GetInt64()).UtcDateTime;
                return (DateTime.UtcNow - messageTime).TotalSeconds > TIME_THRESHOLD_SECONDS;
            }
            return false;
        }

        private async Task ExecuteCommandAsync(string text, long chatId, long userId, string languageCode)
        {
            switch (text.ToLowerInvariant())
            {
                case "/start":
                    await HandleStartCommand(chatId, languageCode);
                    break;
                case "/status":
                    await HandleStatusCommand(chatId, userId, languageCode);
                    break;
                case "/screenshot":
                    await HandleScreenshotCommand(chatId, userId, languageCode);
                    break;
                case "/sleep":
                    await HandleAuthorizedCommand(chatId, userId, languageCode, "sleep", "rundll32.exe powrprof.dll,SetSuspendState 0,1,0");
                    break;
                case "/hibernate":
                    await HandleAuthorizedCommand(chatId, userId, languageCode, "hibernate", "rundll32.exe powrprof.dll,SetSuspendState Hibernate");
                    break;
                case "/shutdown":
                    await HandleAuthorizedCommand(chatId, userId, languageCode, "shutdown", "shutdown /s /t 0");
                    break;
                case "/restart":
                    await HandleAuthorizedCommand(chatId, userId, languageCode, "restart", "shutdown /r /t 0");
                    break;
                default:
                    await SendReplyAsync(chatId, "commands", languageCode);
                    break;
            }
        }

        private bool IsUserAuthorized(long userId)
        {
            return AuthorizedUsers.Contains(userId);
        }

        private string GetTranslation(string languageCode, string key, params object[] args)
        {
            var lang = languageCode.StartsWith("ru") ? "ru" : "en";
            var translations = _translations[lang];
            var message = translations.TryGetValue(key, out var value) ? value : translations["commands"];
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private async Task SendReplyAsync(long chatId, string messageKey, string languageCode, params object[] args)
        {
            var message = GetTranslation(languageCode, messageKey, args);
            await SendMessageAsync(chatId, message);
        }

        private async Task SendMessageAsync(long chatId, string text)
        {
            for (int retry = 0; retry < MAX_RETRY_ATTEMPTS; retry++)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{Token}/sendMessage";
                    var payload = new
                    {
                        chat_id = chatId,
                        text = text,
                        parse_mode = "HTML"
                    };

                    var json = JsonSerializer.Serialize(payload);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var response = await _httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                    return; // Sent successfully
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendMessageRetry"], RETRY_DELAY_MS));
                    await Task.Delay(RETRY_DELAY_MS * (retry + 1)); // Exponential delay
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendMessageError"], retry + 1, ex.Message));
                    if (retry < MAX_RETRY_ATTEMPTS - 1)
                        await Task.Delay(RETRY_DELAY_MS);
                }
            }
        }

        private async Task SendPhotoAsync(long chatId, byte[] photoData, string caption = "")
        {
            for (int retry = 0; retry < MAX_RETRY_ATTEMPTS; retry++)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{Token}/sendPhoto";
                    
                    using var formData = new MultipartFormDataContent();
                    formData.Add(new StringContent(chatId.ToString()), "chat_id");
                    
                    if (!string.IsNullOrEmpty(caption))
                        formData.Add(new StringContent(caption), "caption");
                    
                    using var photoContent = new ByteArrayContent(photoData);
                    photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    formData.Add(photoContent, "photo", $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    using var response = await _httpClient.PostAsync(url, formData);
                    response.EnsureSuccessStatusCode();
                    return; // Sent successfully
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendMessageRetry"], RETRY_DELAY_MS));
                    await Task.Delay(RETRY_DELAY_MS * (retry + 1));
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendPhotoError"], retry + 1, ex.Message));
                    if (retry < MAX_RETRY_ATTEMPTS - 1)
                        await Task.Delay(RETRY_DELAY_MS);
                }
            }
        }

        private async Task HandleAuthorizedCommand(long chatId, long userId, string languageCode, string messageKey, string? systemCommand = null)
        {
            if (!IsUserAuthorized(userId))
            {
                await SendReplyAsync(chatId, "unauthorized", languageCode, userId);
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_UnauthorizedAccess"], userId));
                return;
            }

            await SendReplyAsync(chatId, "command_executing", languageCode);
            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ExecutingCommand"], messageKey));

            try
            {
                await SendReplyAsync(chatId, messageKey, languageCode);
                
                if (!string.IsNullOrEmpty(systemCommand))
                {
                    await Task.Delay(2000); // We give the user time to see the message
                    ExecuteSystemCommand(systemCommand);
                }
            }
            catch (Exception ex)
            {
                await SendReplyAsync(chatId, "error_occurred", languageCode, ex.Message);
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecutionError"], messageKey, ex.Message));
            }
        }

        private async Task HandleStartCommand(long chatId, string languageCode)
        {
            await SendReplyAsync(chatId, "commands", languageCode);
        }

        private async Task HandleStatusCommand(long chatId, long userId, string languageCode)
        {
            if (!IsUserAuthorized(userId))
            {
                await SendReplyAsync(chatId, "unauthorized", languageCode, userId);
                return;
            }

            try
            {
                var status = GetSystemStatus();
                await SendReplyAsync(chatId, "status", languageCode, 
                    status.ComputerName, 
                    status.ProcessorName,
                    status.Uptime.Days, 
                    status.Uptime.Hours, 
                    status.Uptime.Minutes, 
                    status.Uptime.Seconds,
                    status.UsedRamGB,
                    status.TotalRamGB);
            }
            catch (Exception ex)
            {
                await SendReplyAsync(chatId, "error_occurred", languageCode, ex.Message);
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SystemStatusError"], ex.Message));
            }
        }

        private async Task HandleScreenshotCommand(long chatId, long userId, string languageCode)
        {
            if (!IsUserAuthorized(userId))
            {
                await SendReplyAsync(chatId, "unauthorized", languageCode, userId);
                return;
            }

            await SendReplyAsync(chatId, "command_executing", languageCode);
            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ExecutingCommand"], "screenshot"));

            try
            {
                var screenshots = TakeScreenshots();

                if (screenshots.Count == 0)
                {
                    await SendReplyAsync(chatId, "screenshot_error", languageCode, "No screens found");
                    return;
                }

                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_ScreenshotsFound"], screenshots.Count));

                for (int i = 0; i < screenshots.Count; i++)
                {
                    var screenshotData = screenshots[i];

                    // Проверяем размер файла
                    var fileSizeMB = screenshotData.Length / 1024.0 / 1024.0;
                    if (screenshotData.Length > MAX_FILE_SIZE)
                    {
                        await SendReplyAsync(chatId, "file_too_large", languageCode, fileSizeMB, MAX_FILE_SIZE / 1024.0 / 1024.0);
                        continue;
                    }

                    var caption = GetTranslation(languageCode, "screenshot_monitor", i + 1, screenshots.Count);
                    await SendPhotoAsync(chatId, screenshotData, caption);

                    // There is a slight delay between sending screenshots
                    if (i < screenshots.Count - 1)
                        await Task.Delay(500);
                }

                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_AllScreenshotsSent"], screenshots.Count));
            }
            catch (Exception ex)
            {
                await SendReplyAsync(chatId, "screenshot_error", languageCode, ex.Message);
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecutionError"], ex.Message));
            }
        }

        private static List<byte[]> TakeScreenshots()
        {
            var screenshots = new List<byte[]>();

            try
            {
                var screens = Screen.AllScreens.OrderBy(s => s.Bounds.X).ToArray();

                foreach (var screen in screens)
                {
                    var bounds = screen.Bounds;

                    // Create a bitmap taking into account the screen DPI
                    using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                    using var graphics = Graphics.FromImage(bitmap);

                    // Setting DPI for correct rendering
                    var hdc = graphics.GetHdc();
                    try
                    {
                        // Getting screen DPI via WinAPI
                        var dpiX = GetDeviceCaps(hdc, 88); // LOGPIXELSX
                        var dpiY = GetDeviceCaps(hdc, 90); // LOGPIXELSY
                        bitmap.SetResolution(dpiX, dpiY);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }

                    // Setting up rendering quality
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    // We capture the screen taking into account its position
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

                    using var stream = new MemoryStream();

                    // Optimized JPEG settings
                    var quality = Math.Min(95, 100 - (bounds.Width * bounds.Height / 1000000));
                    var encoder = ImageCodecInfo.GetImageDecoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                    bitmap.Save(stream, encoder, encoderParams);
                    screenshots.Add(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to take screenshots: {ex.Message}", ex);
            }

            return screenshots;
        }
        private void ExecuteSystemCommand(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = processInfo };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandOutput"], e.Data));
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandError"], e.Data));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(10000)) // 10 seconds timeout
                {
                    process.Kill();
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandKilled"]));
                    return;
                }

                if (process.ExitCode != 0)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExitCode"], process.ExitCode));
                    if (errorBuilder.Length > 0)
                    {
                        LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandErrorDetails"], errorBuilder.ToString().Trim()));
                    }
                }
                else
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecuted"], command));
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_CommandExecutionError"], ex.Message));
            }
        }

        private static (string ComputerName, string ProcessorName, TimeSpan Uptime, double UsedRamGB, double TotalRamGB) GetSystemStatus()
        {
            string computerName = Environment.MachineName;
            string processorName = "Unknown";
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            double usedRamGB = 0;
            double totalRamGB = 0;

            try
            {
                // Getting information about the processor with additional error handling
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor"))
                {
                    var collection = searcher.Get();
                    if (collection.Count > 0)
                    {
                        var obj = collection.Cast<ManagementObject>().First();
                        processorName = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    }
                }

                // Получаем информацию о памяти
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    var collection = searcher.Get();
                    if (collection.Count > 0)
                    {
                        var obj = collection.Cast<ManagementObject>().First();
                        var totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0);
                        var freeKB = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0);
                        
                        totalRamGB = totalKB / 1024.0 / 1024.0;
                        usedRamGB = totalRamGB - (freeKB / 1024.0 / 1024.0);
                    }
                }
            }
            catch (Exception ex)
            {
                processorName = $"Error: {ex.Message}";
                usedRamGB = 0;
                totalRamGB = 0;
            }

            return (computerName, processorName, uptime, Math.Round(usedRamGB, 1), Math.Round(totalRamGB, 1));
        }

        private static byte[] TakeScreenshot()
        {
            try
            {
                var bounds = Screen.PrimaryScreen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                
                using var stream = new MemoryStream();
                
                // Using JPEG to Reduce File Size
                var encoder = ImageCodecInfo.GetImageDecoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                
                bitmap.Save(stream, encoder, encoderParams);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to take screenshot: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            if (IsRunning)
            {
                try
                {
                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_DisposeError"], ex.Message));
                }
            }
            
            _httpClient?.Dispose();
            _commandSemaphore?.Dispose();
            _cts?.Dispose();
            _disposed = true;
            
            GC.SuppressFinalize(this);
        }
    }
}
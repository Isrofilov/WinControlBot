using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using WinControlBot.Localization;

namespace WinControlBot
{
    public interface ITelegramClient : IDisposable
    {
        string Token { get; set; }
        event Action<string>? LogReceived;
        
        Task<bool> ValidateTokenAsync();
        Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, int timeout, CancellationToken token);
        Task SendMessageAsync(long chatId, string text);
        Task SendMessageWithKeyboardAsync(long chatId, string text, object replyMarkup);
        Task SendPhotoAsync(long chatId, byte[] photoData, string caption = "");
    }

    public class TelegramClient : ITelegramClient
    {
        private readonly HttpClient _httpClient;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 1000;
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB - Telegram limit

        public string Token { get; set; } = "";
        public event Action<string>? LogReceived;

        public TelegramClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(40)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinControlBot/2");
        }

        public async Task<bool> ValidateTokenAsync()
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

        public async Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, int timeout, CancellationToken token)
        {
            var url = $"https://api.telegram.org/bot{Token}/getUpdates?offset={offset}&timeout={timeout}&limit=100";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new TelegramConflictException();
                }
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(token);
            using var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            {
                throw new Exception("Telegram API returned non-ok response");
            }

            var updates = new List<TelegramUpdate>();
            
            if (data.RootElement.TryGetProperty("result", out var resultArray))
            {
                foreach (var update in resultArray.EnumerateArray())
                {
                    var telegramUpdate = ParseUpdate(update);
                    if (telegramUpdate != null)
                    {
                        updates.Add(telegramUpdate);
                    }
                }
            }

            return updates;
        }

        private static TelegramUpdate? ParseUpdate(JsonElement update)
        {
            if (!update.TryGetProperty("update_id", out var updateId))
                return null;

            var result = new TelegramUpdate
            {
                UpdateId = updateId.GetInt64()
            };

            if (update.TryGetProperty("message", out var message))
            {
                result.Message = ParseMessage(message);
            }

            return result;
        }

        private static TelegramMessage? ParseMessage(JsonElement message)
        {
            if (!message.TryGetProperty("chat", out var chat) ||
                !chat.TryGetProperty("id", out var chatIdElem) ||
                !message.TryGetProperty("from", out var from) ||
                !from.TryGetProperty("id", out var userIdElem) ||
                !message.TryGetProperty("text", out var textElem) ||
                !message.TryGetProperty("date", out var dateElem))
            {
                return null;
            }

            return new TelegramMessage
            {
                ChatId = chatIdElem.GetInt64(),
                UserId = userIdElem.GetInt64(),
                Text = textElem.GetString() ?? "",
                Date = dateElem.GetInt64()
            };
        }

        public async Task SendMessageAsync(long chatId, string text)
        {
            await ExecuteWithRetryAsync(async () =>
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
            });
        }

        public async Task SendMessageWithKeyboardAsync(long chatId, string text, object replyMarkup)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var url = $"https://api.telegram.org/bot{Token}/sendMessage";
                var payload = new
                {
                    chat_id = chatId,
                    text = text,
                    parse_mode = "HTML",
                    reply_markup = replyMarkup
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
            });
        }

        public async Task SendPhotoAsync(long chatId, byte[] photoData, string caption = "")
        {
            if (photoData.Length > MAX_FILE_SIZE)
            {
                var fileSizeMB = photoData.Length / 1024.0 / 1024.0;
                await SendMessageAsync(chatId, string.Format(LocalizationManager.Instance["Bot_File_Too_Large"], 
                    fileSizeMB, MAX_FILE_SIZE / 1024.0 / 1024.0));
                return;
            }

            await ExecuteWithRetryAsync(async () =>
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
            });
        }

        private async Task ExecuteWithRetryAsync(Func<Task> action)
        {
            for (int retry = 0; retry < MAX_RETRY_ATTEMPTS; retry++)
            {
                try
                {
                    await action();
                    return; // Success
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendMessageRetry"], RETRY_DELAY_MS));
                    await Task.Delay(RETRY_DELAY_MS * (retry + 1)); // Exponential delay
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_SendMessageError"], retry + 1, ex.Message));
                    if (retry < MAX_RETRY_ATTEMPTS - 1)
                        await Task.Delay(RETRY_DELAY_MS);
                    else
                        throw; // Re-throw on final attempt
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    // Data Transfer Objects
    public class TelegramUpdate
    {
        public long UpdateId { get; set; }
        public TelegramMessage? Message { get; set; }
    }

    public class TelegramMessage
    {
        public long ChatId { get; set; }
        public long UserId { get; set; }
        public string Text { get; set; } = "";
        public long Date { get; set; }
    }

    // Custom exceptions
    public class TelegramConflictException : Exception
    {
        public TelegramConflictException() : base("Telegram API conflict - another instance is running") { }
    }
}
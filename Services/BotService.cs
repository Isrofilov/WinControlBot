using System.Text.Json;
using WinControlBot.Localization;

namespace WinControlBot
{
    public class BotService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly ITelegramClient _telegramClient;
        private readonly ICommandHandler _commandHandler;
        private readonly SemaphoreSlim _commandSemaphore = new(1, 1);
        private long _offset = 0;
        private bool _disposed = false;

        private const int TIME_THRESHOLD_SECONDS = 300; // 5 minutes
        private const int REQUEST_TIMEOUT_SECONDS = 30;

        public string Token { get; set; } = "";
        public long[] AuthorizedUsers { get; set; } = Array.Empty<long>();
        public bool IsRunning { get; private set; }

        public event Action<string>? LogReceived;
        public event Action<bool>? StatusChanged;

        public BotService(ITelegramClient? telegramClient = null, ICommandHandler? commandHandler = null)
        {
            _telegramClient = telegramClient ?? new TelegramClient();
            _commandHandler = commandHandler ?? new CommandHandler();
            
            // Subscribe to events
            _telegramClient.LogReceived += (message) => LogReceived?.Invoke(message);
            _commandHandler.LogReceived += (message) => LogReceived?.Invoke(message);
        }

        public async Task<bool> StartAsync()
        {
            if (IsRunning || string.IsNullOrEmpty(Token))
                return false;

            _telegramClient.Token = Token;

            if (!await _telegramClient.ValidateTokenAsync())
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
                await Task.Delay(100);
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

        private async Task PollUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var updates = await _telegramClient.GetUpdatesAsync(_offset, REQUEST_TIMEOUT_SECONDS, token);
                    
                    foreach (var update in updates)
                    {
                        _offset = update.UpdateId + 1;
                        
                        if (update.Message != null)
                        {
                            await ProcessMessageAsync(update.Message);
                        }
                    }
                }
                catch (TaskCanceledException) when (token.IsCancellationRequested) { }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (TelegramConflictException)
                {
                    LogReceived?.Invoke(LocalizationManager.Instance["Bot_Conflict"]);
                    await StopAsync();
                    return;
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_PollError"], ex.Message));
                    await Task.Delay(1000, token);
                }
            }
        }

        private async Task ProcessMessageAsync(TelegramMessage message)
        {
            if (IsMessageTooOld(message))
            {
                await _telegramClient.SendMessageAsync(message.ChatId, LocalizationManager.Instance["Bot_Retry_Request"]);
                return;
            }

            LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_MessageReceived"], message.UserId, message.Text));

            await _commandSemaphore.WaitAsync();
            try
            {
                var context = new CommandContext
                {
                    Message = message,
                    IsUserAuthorized = AuthorizedUsers.Contains(message.UserId),
                    TelegramClient = _telegramClient
                };

                await _commandHandler.HandleCommandAsync(context);
            }
            finally
            {
                _commandSemaphore.Release();
            }
        }

        private static bool IsMessageTooOld(TelegramMessage message)
        {
            var messageTime = DateTimeOffset.FromUnixTimeSeconds(message.Date).UtcDateTime;
            return (DateTime.UtcNow - messageTime).TotalSeconds > TIME_THRESHOLD_SECONDS;
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
            
            _telegramClient?.Dispose();
            _commandSemaphore?.Dispose();
            _cts?.Dispose();
            _disposed = true;
            
            GC.SuppressFinalize(this);
        }
    }
}
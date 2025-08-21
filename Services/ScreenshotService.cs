using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WinControlBot.Localization;

namespace WinControlBot
{
    public interface IScreenshotService
    {
        event Action<string>? LogReceived;
        Task<List<byte[]>> TakeScreenshotsAsync();
    }

    public class ScreenshotService : IScreenshotService
    {
        public event Action<string>? LogReceived;

        public async Task<List<byte[]>> TakeScreenshotsAsync()
        {
            return await Task.Run(() =>
            {
                var screenshots = new List<byte[]>();

                try
                {
                    foreach (var screen in Screen.AllScreens.OrderBy(s => s.Bounds.X))
                    {
                        var bounds = screen.Bounds;
                        
                        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                        using var graphics = Graphics.FromImage(bitmap);
                        
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                        
                        using var stream = new MemoryStream();
                        bitmap.Save(stream, ImageFormat.Jpeg);
                        
                        screenshots.Add(stream.ToArray());
                        
                        LogReceived?.Invoke($"Screenshot taken for screen {screen.DeviceName} ({bounds.Width}x{bounds.Height})");
                    }
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke(string.Format(LocalizationManager.Instance["Bot_Screenshot_Error"], ex.Message));
                    throw;
                }

                return screenshots;
            });
        }
    }
}
using System.Diagnostics;
using System.Management;
using System.Text;
using WinControlBot.Localization;

namespace WinControlBot
{
    public interface ISystemService
    {
        event Action<string>? LogReceived;
        Task<SystemStatus> GetSystemStatusAsync();
        Task ExecuteCommandAsync(string command);
    }

    public class SystemService : ISystemService
    {
        public event Action<string>? LogReceived;

        public async Task<SystemStatus> GetSystemStatusAsync()
        {
            return await Task.Run(() =>
            {
                string computerName = Environment.MachineName;
                string processorName = "Unknown";
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                double usedRamGB = 0;
                double totalRamGB = 0;

                try
                {
                    // Get processor information
                    processorName = GetProcessorName();
                    
                    // Get memory information
                    (usedRamGB, totalRamGB) = GetMemoryInfo();
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"Error getting system status: {ex.Message}");
                    processorName = $"Error: {ex.Message}";
                    usedRamGB = 0;
                    totalRamGB = 0;
                }

                return new SystemStatus
                {
                    ComputerName = computerName,
                    ProcessorName = processorName,
                    Uptime = uptime,
                    UsedRamGB = Math.Round(usedRamGB, 1),
                    TotalRamGB = Math.Round(totalRamGB, 1)
                };
            });
        }

        private static string GetProcessorName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor");
                var collection = searcher.Get();
                
                if (collection.Count > 0)
                {
                    var obj = collection.Cast<ManagementObject>().First();
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }

            return "Unknown";
        }

        private static (double UsedRamGB, double TotalRamGB) GetMemoryInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", 
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                var collection = searcher.Get();
                
                if (collection.Count > 0)
                {
                    var obj = collection.Cast<ManagementObject>().First();
                    var totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0);
                    var freeKB = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0);
                    
                    var totalRamGB = totalKB / 1024.0 / 1024.0;
                    var usedRamGB = totalRamGB - (freeKB / 1024.0 / 1024.0);
                    
                    return (usedRamGB, totalRamGB);
                }
            }
            catch (Exception)
            {
                // Ignore errors and return zeros
            }

            return (0, 0);
        }

        public async Task ExecuteCommandAsync(string command)
        {
            await Task.Run(() =>
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
                        LogReceived?.Invoke(LocalizationManager.Instance["Bot_CommandKilled"]);
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
            });
        }
    }

    // Data Transfer Object for system status
    public class SystemStatus
    {
        public string ComputerName { get; set; } = "";
        public string ProcessorName { get; set; } = "";
        public TimeSpan Uptime { get; set; }
        public double UsedRamGB { get; set; }
        public double TotalRamGB { get; set; }
    }
}
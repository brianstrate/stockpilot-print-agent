using System.IO;
using System.Text.Json;

namespace StockPilotPrintAgent.Shared;

public class ServiceStatusSnapshot
{
    public string StateText { get; set; } = "Afventer opsætning";
    public string BadgeColor { get; set; } = "#D97706";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<string> Printers { get; set; } = new();
}

public static class SharedLogService
{
    private static readonly object FileLock = new();
    public static readonly string LogPath = Path.Combine(SharedConfigService.BaseDir, "service.log");
    public static readonly string StatusPath = Path.Combine(SharedConfigService.BaseDir, "status.json");

    public static void WriteLog(string message)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(SharedConfigService.BaseDir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);

                // Trim logfil hvis den overstiger 250 KB
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 250_000)
                {
                    var lines = File.ReadAllLines(LogPath);
                    File.WriteAllLines(LogPath, lines.Skip(Math.Max(0, lines.Length - 250)));
                }
            }
        }
        catch
        {
            // Ignorer låsefejl
        }
    }

    public static List<string> ReadRecentLines(int maxLines = 150)
    {
        try
        {
            lock (FileLock)
            {
                if (!File.Exists(LogPath))
                    return new List<string>();

                var lines = File.ReadAllLines(LogPath);
                return lines.TakeLast(maxLines).Reverse().ToList();
            }
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void WriteStatus(string stateText, string hexColor, List<string> printers)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(SharedConfigService.BaseDir);
                var snap = new ServiceStatusSnapshot
                {
                    StateText = stateText,
                    BadgeColor = hexColor,
                    UpdatedAt = DateTime.Now,
                    Printers = printers
                };
                File.WriteAllText(StatusPath, JsonSerializer.Serialize(snap));
            }
        }
        catch
        {
        }
    }

    public static ServiceStatusSnapshot ReadStatus()
    {
        try
        {
            lock (FileLock)
            {
                if (File.Exists(StatusPath))
                {
                    var json = File.ReadAllText(StatusPath);
                    return JsonSerializer.Deserialize<ServiceStatusSnapshot>(json) ?? new ServiceStatusSnapshot();
                }
            }
        }
        catch
        {
        }
        return new ServiceStatusSnapshot();
    }
}
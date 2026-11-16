using System.IO;
using System.Text.Json;
using StockPilotPrintAgent.Models;

namespace StockPilotPrintAgent.Services;

public static class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StockPilotPrintAgent");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
                if (cfg != null)
                {
                    if (string.IsNullOrWhiteSpace(cfg.MachineId))
                        cfg.MachineId = Guid.NewGuid().ToString("N");
                    return cfg;
                }
            }
        }
        catch
        {
            // Fald tilbage til standardkonfiguration
        }

        var fresh = new AgentConfig();
        Save(fresh);
        return fresh;
    }

    public static void Save(AgentConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
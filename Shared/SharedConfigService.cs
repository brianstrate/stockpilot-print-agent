using System.IO;
using System.Text.Json;

namespace WooRackPrintAgent.Shared;

public static class SharedConfigService
{
    public static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WooRackPrintAgent");

    public static readonly string ConfigPath = Path.Combine(BaseDir, "config.json");

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
            // Fald tilbage til standard
        }

        var fresh = new AgentConfig();
        Save(fresh);
        return fresh;
    }

    public static void Save(AgentConfig config)
    {
        Directory.CreateDirectory(BaseDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
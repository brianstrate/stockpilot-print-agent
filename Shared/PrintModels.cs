using System.Text.Json.Serialization;

namespace StockPilotPrintAgent.Shared;

public class AgentConfig
{
    public string StoreUrl { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string AgentName { get; set; } = Environment.MachineName;
    public string MachineId { get; set; } = Guid.NewGuid().ToString("N");
    public int PollIntervalMs { get; set; } = 1500;
}

public class RegisterAgentRequest
{
    [JsonPropertyName("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("printers")]
    public List<string> Printers { get; set; } = new();
}

public class PollResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("job")]
    public PrintJobDto? Job { get; set; }
}

public class PrintJobDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("printer_name")]
    public string PrinterName { get; set; } = "";

    [JsonPropertyName("html")]
    public string Html { get; set; } = "";

    [JsonPropertyName("width_mm")]
    public double WidthMm { get; set; } = 80;

    [JsonPropertyName("height_mm")]
    public double? HeightMm { get; set; }

    [JsonPropertyName("copies")]
    public int Copies { get; set; } = 1;
}

public class AckJobRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "printed";

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
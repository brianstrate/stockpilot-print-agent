using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using StockPilotPrintAgent.Models;
using StockPilotPrintAgent.Services;

namespace StockPilotPrintAgent;

public partial class MainWindow : Window
{
    public AgentConfig Config { get; private set; }
    private readonly PrintApiClient _apiClient = new();
    private readonly SilentHtmlPrinter _printer = new();
    private CancellationTokenSource? _cts;
    private bool _isRegistered;

    public MainWindow()
    {
        InitializeComponent();
        Config = ConfigService.Load();

        StoreUrlBox.Text = Config.StoreUrl;
        ApiTokenBox.Text = Config.ApiToken;
        AgentNameBox.Text = Config.AgentName;
        StartMinimizedCheck.IsChecked = Config.StartMinimized;

        RefreshPrinterListUi();
    }

    private List<string> RefreshPrinterListUi()
    {
        var printers = SilentHtmlPrinter.GetInstalledPrinters();
        PrintersSummaryText.Text = printers.Count > 0
            ? string.Join("  •  ", printers)
            : "Ingen lokale Windows-printere fundet.";
        return printers;
    }

    public void StartBackgroundPolling()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
    }

    public async Task ForceSyncPrintersAsync()
    {
        var printers = RefreshPrinterListUi();
        if (string.IsNullOrWhiteSpace(Config.StoreUrl) || string.IsNullOrWhiteSpace(Config.ApiToken))
        {
            AppendLog("Mangler WordPress URL eller API Token.");
            return;
        }

        try
        {
            await _apiClient.RegisterAgentAsync(Config, printers);
            _isRegistered = true;
            SetStatus("Online & Klar", "#16A34A");
            AppendLog($"Synkroniserede {printers.Count} printer(e) med WordPress.");
        }
        catch (Exception ex)
        {
            _isRegistered = false;
            SetStatus("Forbindelsesfejl", "#DC2626");
            AppendLog($"Registrering fejlede: {ex.Message}");
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(Config.StoreUrl) || string.IsNullOrWhiteSpace(Config.ApiToken))
            {
                SetStatus("Afventer opsætning", "#D97706");
                await Task.Delay(2000, ct);
                continue;
            }

            try
            {
                if (!_isRegistered)
                {
                    var printers = RefreshPrinterListUi();
                    await _apiClient.RegisterAgentAsync(Config, printers, ct);
                    _isRegistered = true;
                    SetStatus("Online & Klar", "#16A34A");
                    AppendLog($"Forbundet til {Config.StoreUrl} ({printers.Count} printere registreret).");
                }

                var job = await _apiClient.PollNextJobAsync(Config, ct);
                if (job != null)
                {
                    AppendLog($"Modtog printjob #{job.Id} -> Printer: '{job.PrinterName}' ({job.WidthMm} mm)...");
                    try
                    {
                        await _printer.PrintJobAsync(job);
                        await _apiClient.AcknowledgeJobAsync(Config, job.Id, true, null, ct);
                        AppendLog($"Job #{job.Id} udskrevet uden fejl.");
                    }
                    catch (Exception printEx)
                    {
                        AppendLog($"FEJL ved print af job #{job.Id}: {printEx.Message}");
                        await _apiClient.AcknowledgeJobAsync(Config, job.Id, false, printEx.Message, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _isRegistered = false;
                SetStatus("Offline (prøver igen...)", "#DC2626");
                AppendLog($"Polling-fejl: {ex.Message}");
                await Task.Delay(3000, ct);
            }

            await Task.Delay(Math.Max(500, Config.PollIntervalMs), ct);
        }
    }

    private async void SaveConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        Config.StoreUrl = StoreUrlBox.Text.Trim();
        Config.ApiToken = ApiTokenBox.Text.Trim();
        Config.AgentName = string.IsNullOrWhiteSpace(AgentNameBox.Text)
            ? Environment.MachineName
            : AgentNameBox.Text.Trim();
        Config.StartMinimized = StartMinimizedCheck.IsChecked == true;

        ConfigService.Save(Config);
        _isRegistered = false;
        AppendLog("Indstillinger gemt.");
        await ForceSyncPrintersAsync();
    }

    private async void RefreshPrintersBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshPrinterListUi();
        await ForceSyncPrintersAsync();
    }

   private void SetStatus(string text, string hexColor)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            StatusBadge.Background = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
        });
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogListBox.Items.Insert(0, line);
            while (LogListBox.Items.Count > 200)
                LogListBox.Items.RemoveAt(LogListBox.Items.Count - 1);
        });
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Minimer til systembakken i stedet for at lukke programmet
        e.Cancel = true;
        Hide();
    }

    public async Task StopAndDisposeAsync()
    {
        _cts?.Cancel();
        await _printer.DisposeAsync();
    }
}
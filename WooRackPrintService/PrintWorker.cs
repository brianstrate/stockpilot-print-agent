using Microsoft.Extensions.Hosting;
using WooRackPrintAgent.Shared;

namespace WooRackPrintService;

public class PrintWorker : BackgroundService
{
    private readonly PrintApiClient _apiClient;
    private readonly Session0HtmlPrinter _printer;
    private bool _isRegistered;

    public PrintWorker(PrintApiClient apiClient, Session0HtmlPrinter printer)
    {
        _apiClient = apiClient;
        _printer = printer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SharedLogService.WriteLog("WooRack Print Service startet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var config = SharedConfigService.Load();
            var printers = Session0HtmlPrinter.GetInstalledPrinters();

            if (string.IsNullOrWhiteSpace(config.StoreUrl) || string.IsNullOrWhiteSpace(config.ApiToken))
            {
                SharedLogService.WriteStatus("Afventer opsætning", "#D97706", printers);
                await Task.Delay(2500, stoppingToken);
                continue;
            }

            try
            {
                if (!_isRegistered)
                {
                    await _apiClient.RegisterAgentAsync(config, printers, stoppingToken);
                    _isRegistered = true;
                    SharedLogService.WriteStatus("Online & Klar (Service)", "#16A34A", printers);
                    SharedLogService.WriteLog($"Forbundet til {config.StoreUrl} ({printers.Count} printere registreret).");
                }

                var job = await _apiClient.PollNextJobAsync(config, stoppingToken);
                if (job != null)
                {
                    SharedLogService.WriteLog($"Modtog printjob #{job.Id} -> Printer: '{job.PrinterName}' ({job.WidthMm} mm)...");
                    try
                    {
                        await _printer.PrintJobAsync(job, stoppingToken);
                        await _apiClient.AcknowledgeJobAsync(config, job.Id, true, null, stoppingToken);
                        SharedLogService.WriteLog($"Job #{job.Id} udskrevet uden fejl.");
                    }
                    catch (Exception printEx)
                    {
                        SharedLogService.WriteLog($"FEJL ved print af job #{job.Id}: {printEx.Message}");
                        await _apiClient.AcknowledgeJobAsync(config, job.Id, false, printEx.Message, stoppingToken);
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
                SharedLogService.WriteStatus("Offline (prøver igen...)", "#DC2626", printers);
                SharedLogService.WriteLog($"Polling-fejl: {ex.Message}");
                await Task.Delay(3000, stoppingToken);
            }

            await Task.Delay(Math.Max(500, config.PollIntervalMs), stoppingToken);
        }

        SharedLogService.WriteStatus("Service stoppet", "#64748B", Session0HtmlPrinter.GetInstalledPrinters());
        SharedLogService.WriteLog("WooRack Print Service stoppet.");
    }
}
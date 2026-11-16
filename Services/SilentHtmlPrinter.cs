using System.Drawing.Printing;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StockPilotPrintAgent.Models;
using WpfApplication = System.Windows.Application;

namespace StockPilotPrintAgent.Services;

public class SilentHtmlPrinter : IAsyncDisposable
{
    private WebView2? _webView;
    private bool _initialized;

    public static List<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            list.Add(printer);
        }
        return list;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await WpfApplication.Current.Dispatcher.InvokeAsync(async () =>
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StockPilotPrintAgent",
                "WebView2");

            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            _webView = new WebView2();
            await _webView.EnsureCoreWebView2Async(env);
            _initialized = true;
        }).Task.Unwrap();
    }

    public async Task PrintJobAsync(PrintJobDto job)
    {
        await InitializeAsync();

        var targetPrinter = ResolvePrinterName(job.PrinterName);

        await WpfApplication.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null)
                throw new InvalidOperationException("WebView2 er ikke initialiseret.");

            var navCompleted = new TaskCompletionSource<bool>();

            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                if (e.IsSuccess)
                    navCompleted.TrySetResult(true);
                else
                    navCompleted.TrySetException(new Exception($"HTML-indlæsning fejlede ({e.WebErrorStatus})."));
            }

            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.NavigateToString(job.Html);

            await navCompleted.Task;

            // Kort ventetid så billeder/stregkoder/fonts er færdigrenderet
            await Task.Delay(250);

            var printSettings = _webView.CoreWebView2.Environment.CreatePrintSettings();
            printSettings.PrinterName = targetPrinter;
            printSettings.ShouldPrintBackgrounds = true;
            printSettings.ShouldPrintHeaderAndFooter = false;
            printSettings.ShouldPrintSelectionOnly = false;
            printSettings.MarginTop = 0;
            printSettings.MarginBottom = 0;
            printSettings.MarginLeft = 0;
            printSettings.MarginRight = 0;
            printSettings.Copies = Math.Max(1, job.Copies);

            if (job.WidthMm > 0)
            {
                printSettings.PageWidth = job.WidthMm / 25.4;
            }
            if (job.HeightMm.HasValue && job.HeightMm.Value > 0)
            {
                printSettings.PageHeight = job.HeightMm.Value / 25.4;
            }

            var status = await _webView.CoreWebView2.PrintAsync(printSettings);
            if (status != CoreWebView2PrintStatus.Succeeded)
            {
                throw new Exception($"Printer '{targetPrinter}' returnerede status: {status}");
            }
        }).Task.Unwrap();
    }

    private static string ResolvePrinterName(string requestedPrinter)
    {
        var installed = GetInstalledPrinters();
        if (installed.Count == 0)
            throw new InvalidOperationException("Ingen printere er installeret på denne Windows-maskine.");

        if (!string.IsNullOrWhiteSpace(requestedPrinter))
        {
            var exact = installed.FirstOrDefault(p => p.Equals(requestedPrinter, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }

        var defaultSettings = new PrinterSettings();
        if (!string.IsNullOrWhiteSpace(defaultSettings.PrinterName))
            return defaultSettings.PrinterName;

        return installed[0];
    }

    public ValueTask DisposeAsync()
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            _webView?.Dispose();
            _webView = null;
        });
        return ValueTask.CompletedTask;
    }
}
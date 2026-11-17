using System.Diagnostics;
using System.Drawing.Printing;
using WooRackPrintAgent.Shared;

namespace WooRackPrintService;

public class Session0HtmlPrinter
{
    public static List<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            list.Add(printer);
        }
        return list;
    }

    public async Task PrintJobAsync(PrintJobDto job, CancellationToken ct)
    {
        var targetPrinter = ResolvePrinterName(job.PrinterName);
        var edgePath = FindEdgeExecutable();
        var sumatraPath = FindSumatraExecutable();

        var workDir = Path.Combine(SharedConfigService.BaseDir, "temp");
        Directory.CreateDirectory(workDir);

        var token = $"{job.Id}_{Guid.NewGuid():N}";
        var htmlFile = Path.Combine(workDir, $"{token}.html");
        var pdfFile = Path.Combine(workDir, $"{token}.pdf");

        try
        {
            await File.WriteAllTextAsync(htmlFile, job.Html, ct);

            // 1. Konverter HTML til PDF via Edge Headless (fungerer i Session 0 uden bruger-login)
            var fileUri = new Uri(htmlFile).AbsoluteUri;
            var edgeArgs = $"--headless --disable-gpu --no-sandbox --disable-software-rasterizer --no-pdf-header-footer --print-to-pdf=\"{pdfFile}\" \"{fileUri}\"";

            await RunProcessAsync(edgePath, edgeArgs, TimeSpan.FromSeconds(20), ct);

            if (!File.Exists(pdfFile))
                throw new FileNotFoundException("Edge Headless genererede ikke PDF-filen til udskrift.");

            // 2. Udskriv PDF lydløst til Windows-printeren via SumatraPDF
            var copies = Math.Max(1, job.Copies);
            var sumatraArgs = $"-print-to \"{targetPrinter}\" -print-settings \"noscale,portrait,{copies}x\" -silent \"{pdfFile}\"";

            await RunProcessAsync(sumatraPath, sumatraArgs, TimeSpan.FromSeconds(20), ct);
        }
        finally
        {
            TryDelete(htmlFile);
            TryDelete(pdfFile);
        }
    }

    private static string ResolvePrinterName(string requestedPrinter)
    {
        var installed = GetInstalledPrinters();
        if (installed.Count == 0)
            throw new InvalidOperationException("Ingen printere er installeret på Windows-maskinen.");

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

    private static string FindEdgeExecutable()
    {
        var candidates = new[]
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found == null)
            throw new FileNotFoundException("Microsoft Edge (msedge.exe) blev ikke fundet på maskinen.");

        return found;
    }

    private static string FindSumatraExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "tools", "SumatraPDF.exe"),
            Path.Combine(baseDir, "SumatraPDF.exe"),
            @"C:\Program Files\SumatraPDF\SumatraPDF.exe",
            @"C:\Program Files (x86)\SumatraPDF\SumatraPDF.exe"
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found == null)
            throw new FileNotFoundException("SumatraPDF.exe blev ikke fundet i service\\tools\\SumatraPDF.exe.");

        return found;
    }

    private static async Task RunProcessAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        proc.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Proces '{Path.GetFileName(fileName)}' timede ud.");
        }

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new Exception($"{Path.GetFileName(fileName)} afsluttede med kode {proc.ExitCode}: {err}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
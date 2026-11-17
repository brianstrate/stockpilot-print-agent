using System.Drawing.Printing;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WooRackPrintAgent.Shared;

namespace WooRackPrintConfig;

public partial class MainWindow : Window
{
    private const string ServiceName = "WooRackPrintAgent";
    private AgentConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly UpdateService _updateService = new();
    private UpdateInfo? _pendingUpdate;

    public MainWindow()
    {
        InitializeComponent();

        var ver = UpdateService.GetCurrentAppVersion();
        VersionText.Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
        Title = $"WooRack Print Agent v{ver.Major}.{ver.Minor}.{ver.Build} — Service Konfigurator";

        _config = SharedConfigService.Load();
        StoreUrlBox.Text = _config.StoreUrl;
        ApiTokenBox.Text = _config.ApiToken;
        AgentNameBox.Text = _config.AgentName;

        RefreshPrintersUi();
        RefreshLiveStatusAndLog();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += (_, _) => RefreshLiveStatusAndLog();
        _refreshTimer.Start();

        _ = CheckForUpdatesAsync(manual: false);
    }

    private void RefreshPrintersUi()
    {
        var printers = new List<string>();
        foreach (string p in PrinterSettings.InstalledPrinters)
            printers.Add(p);

        PrintersSummaryText.Text = printers.Count > 0
            ? string.Join("  •  ", printers)
            : "Ingen lokale Windows-printere fundet.";
    }

    private void RefreshLiveStatusAndLog()
    {
        // 1. Tjek Windows Service tilstand
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            var running = sc.Status == ServiceControllerStatus.Running;
            WinServiceStateText.Text = $"Windows Service: {sc.Status}";
            WinServiceStateText.Foreground = new SolidColorBrush(
                running ? Color.FromRgb(22, 163, 74) : Color.FromRgb(220, 38, 38));

            StartServiceBtn.IsEnabled = sc.Status == ServiceControllerStatus.Stopped;
            StopServiceBtn.IsEnabled = running;
        }
        catch
        {
            WinServiceStateText.Text = "Windows Service: Ikke installeret";
            WinServiceStateText.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
            StartServiceBtn.IsEnabled = false;
            StopServiceBtn.IsEnabled = false;
        }

        // 2. Læs forbindelsesstatus fra status.json
        var snap = SharedLogService.ReadStatus();
        StatusText.Text = snap.StateText;
        try
        {
            StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(snap.BadgeColor));
        }
        catch
        {
            StatusBadge.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        }

        // 3. Opdater loglinjer
        var lines = SharedLogService.ReadRecentLines(150);
        LogListBox.ItemsSource = lines;
    }

    private async void SaveRestartBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveRestartBtn.IsEnabled = false;
        try
        {
            _config.StoreUrl = StoreUrlBox.Text.Trim();
            _config.ApiToken = ApiTokenBox.Text.Trim();
            _config.AgentName = string.IsNullOrWhiteSpace(AgentNameBox.Text)
                ? Environment.MachineName
                : AgentNameBox.Text.Trim();

            SharedConfigService.Save(_config);
            SharedLogService.WriteLog("Konfiguration gemt via GUI.");

            await Task.Run(() =>
            {
                using var sc = new ServiceController(ServiceName);
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            });

            RefreshPrintersUi();
            RefreshLiveStatusAndLog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Indstillinger blev gemt i ProgramData, men servicen kunne ikke genstartes:\n{ex.Message}",
                "Service-advarsel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            SaveRestartBtn.IsEnabled = true;
        }
    }

    private async void StartServiceBtn_Click(object sender, RoutedEventArgs e)
    {
        StartServiceBtn.IsEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                using var sc = new ServiceController(ServiceName);
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            });
            RefreshLiveStatusAndLog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kunne ikke starte servicen: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopServiceBtn_Click(object sender, RoutedEventArgs e)
    {
        StopServiceBtn.IsEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                using var sc = new ServiceController(ServiceName);
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            });
            RefreshLiveStatusAndLog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kunne ikke stoppe servicen: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            if (manual) CheckUpdateBtn.IsEnabled = false;
            var info = await _updateService.CheckForUpdateAsync();
            if (info.IsUpdateAvailable)
            {
                _pendingUpdate = info;
                UpdateTitleText.Text = $"Ny version klar: {info.TagName} (du har v{info.CurrentVersion})";
                UpdateBanner.Visibility = Visibility.Visible;
            }
            else if (manual)
            {
                MessageBox.Show($"Du kører allerede den nyeste version (v{info.CurrentVersion}).", "Opdatering", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (manual)
                MessageBox.Show($"Kunne ikke tjekke for opdateringer: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (manual) CheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(manual: true);
    }

    private async void InstallUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        InstallUpdateBtn.IsEnabled = false;
        InstallUpdateBtn.Content = "Henter (0%)...";

        try
        {
            var progress = new Progress<int>(pct => InstallUpdateBtn.Content = $"Henter ({pct}%)...");
            await _updateService.DownloadAndInstallAsync(_pendingUpdate, progress);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Opdatering fejlede: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
            InstallUpdateBtn.IsEnabled = true;
            InstallUpdateBtn.Content = "Opdater nu";
        }
    }
}
using WooRackPrintService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WooRackPrintAgent";
});

builder.Services.AddSingleton<PrintApiClient>();
builder.Services.AddSingleton<Session0HtmlPrinter>();
builder.Services.AddHostedService<PrintWorker>();

var host = builder.Build();
host.Run();
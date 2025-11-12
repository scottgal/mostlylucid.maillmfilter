using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Services;
using MostlyLucid.MailLLMFilter.App.ViewModels;
using MostlyLucid.MailLLMFilter.App.Services;
using System.IO;
using System.Windows;

namespace MostlyLucid.MailLLMFilter.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MostlyLucid.MailLLMFilter"
                );

                Directory.CreateDirectory(appDataPath);

                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile(Path.Combine(appDataPath, "appsettings.json"), optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<FilterConfiguration>(context.Configuration.GetSection("FilterConfiguration"));

                // Core services
                services.AddSingleton<ILlmService, OllamaLlmService>();

                // Email service - register based on configuration
                var filterConfig = context.Configuration.GetSection("FilterConfiguration").Get<FilterConfiguration>();
                var emailProvider = filterConfig?.EmailProvider?.ToLowerInvariant() ?? "gmail";

                if (emailProvider == "imap")
                {
                    services.AddSingleton<IEmailService, ImapEmailService>();
                }
                else
                {
                    // Default to Gmail
                    services.AddSingleton<Core.Services.GmailService>();
                    services.AddSingleton<IGmailService>(sp => sp.GetRequiredService<Core.Services.GmailService>());
                    services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<Core.Services.GmailService>());
                }

                services.AddSingleton<IFilterEngine, FilterEngine>();
                services.AddSingleton<IEmailSummarizerService, EmailSummarizerService>();

                // App services
                services.AddSingleton<NotificationService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<TemplateTestViewModel>();

                // Logging
                services.AddLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        var mainWindow = new Views.MainWindow
        {
            DataContext = _host.Services.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}

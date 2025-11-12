using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MostlyLucid.MailLLMFilter.Core.Models;
using MostlyLucid.MailLLMFilter.Core.Services;
using MostlyLucid.MailLLMFilter.App.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace MostlyLucid.MailLLMFilter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGmailService _gmailService;
    private readonly ILlmService _llmService;
    private readonly IFilterEngine _filterEngine;
    private readonly NotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainViewModel> _logger;

    private System.Threading.Timer? _autoCheckTimer;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _autoCheckEnabled;

    [ObservableProperty]
    private int _autoCheckInterval = 60;

    [ObservableProperty]
    private string _statusMessage = "Not connected";

    [ObservableProperty]
    private ObservableCollection<FilterResultViewModel> _filterResults = new();

    [ObservableProperty]
    private FilterResultViewModel? _selectedResult;

    public MainViewModel(
        IGmailService gmailService,
        ILlmService llmService,
        IFilterEngine filterEngine,
        NotificationService notificationService,
        IServiceProvider serviceProvider,
        ILogger<MainViewModel> logger)
    {
        _gmailService = gmailService;
        _llmService = llmService;
        _filterEngine = filterEngine;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Connecting to Gmail...";

            await _gmailService.InitializeAsync();

            IsConnected = true;
            StatusMessage = "Connected to Gmail";

            _notificationService.ShowInfoNotification("Gmail Connected", "Successfully connected to Gmail");
            _logger.LogInformation("Successfully connected to Gmail");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Gmail");
            StatusMessage = $"Connection failed: {ex.Message}";
            MessageBox.Show($"Failed to connect to Gmail:\n\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanProcessMessages))]
    private async Task ProcessMessagesAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Processing unread messages...";
            FilterResults.Clear();

            var results = await _filterEngine.ProcessUnreadMessagesAsync();

            foreach (var result in results)
            {
                FilterResults.Add(new FilterResultViewModel(result));

                // Show notification for each filtered message
                if (result.IsMatch)
                {
                    _notificationService.ShowFilteredMessageNotification(result);
                }
            }

            var matchedCount = results.Count(r => r.IsMatch);
            StatusMessage = $"Processed {results.Count} messages ({matchedCount} matched, {results.Count - matchedCount} unmatched)";

            // Show batch notification if there were multiple messages
            if (results.Count > 0)
            {
                _notificationService.ShowBatchProcessedNotification(
                    results.Count,
                    matchedCount,
                    results.Count - matchedCount);
            }

            _logger.LogInformation("Processed {Total} messages: {Matched} matched", results.Count, matchedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing messages");
            StatusMessage = $"Error: {ex.Message}";
            _notificationService.ShowErrorNotification("Processing Error", ex.Message);
            MessageBox.Show($"Error processing messages:\n\n{ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ToggleAutoCheck()
    {
        if (AutoCheckEnabled)
        {
            StartAutoCheck();
        }
        else
        {
            StopAutoCheck();
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        // TODO: Open settings window
        MessageBox.Show("Settings window not yet implemented", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenTemplateTester()
    {
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<TemplateTestViewModel>();
            var testerWindow = new Views.TemplateTestWindow
            {
                DataContext = viewModel
            };
            testerWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening template tester");
            MessageBox.Show($"Error opening template tester:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanProcessMessages() => IsConnected && !IsProcessing;

    private void StartAutoCheck()
    {
        if (_autoCheckTimer != null)
        {
            StopAutoCheck();
        }

        var interval = TimeSpan.FromSeconds(AutoCheckInterval);
        _autoCheckTimer = new System.Threading.Timer(
            async _ => await ProcessMessagesAsync(),
            null,
            interval,
            interval);

        StatusMessage = $"Auto-check enabled (every {AutoCheckInterval} seconds)";
        _logger.LogInformation("Auto-check enabled with interval {Interval} seconds", AutoCheckInterval);
    }

    private void StopAutoCheck()
    {
        _autoCheckTimer?.Dispose();
        _autoCheckTimer = null;

        StatusMessage = "Auto-check disabled";
        _logger.LogInformation("Auto-check disabled");
    }

    partial void OnAutoCheckIntervalChanged(int value)
    {
        if (AutoCheckEnabled)
        {
            StartAutoCheck();
        }
    }
}

public class FilterResultViewModel : ObservableObject
{
    public FilterResult Result { get; }

    public string MessageId => Result.Message.Id;
    public string From => Result.Message.FromName ?? Result.Message.From;
    public string Subject => Result.Message.Subject;
    public string Status => Result.IsMatch ? $"Matched ({Result.Confidence:P0})" : "No Match";
    public string Action => Result.ActionDescription ?? "No action";
    public string RuleName => Result.MatchedRule?.Name ?? "N/A";
    public string StatusColor => Result.IsMatch ? "#4CAF50" : "#9E9E9E";

    public FilterResultViewModel(FilterResult result)
    {
        Result = result;
    }
}

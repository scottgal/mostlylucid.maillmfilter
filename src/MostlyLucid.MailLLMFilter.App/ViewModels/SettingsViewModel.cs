using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MostlyLucid.MailLLMFilter.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly string _configPath;

    [ObservableProperty]
    private FilterConfiguration _filterConfig = new();

    [ObservableProperty]
    private string _selectedEmailProvider = "Gmail";

    [ObservableProperty]
    private ObservableCollection<string> _emailProviders = new() { "Gmail", "IMAP" };

    [ObservableProperty]
    private ObservableCollection<LlmFilterTemplate> _llmTemplates = new();

    [ObservableProperty]
    private ObservableCollection<FilterRule> _filterRules = new();

    [ObservableProperty]
    private ObservableCollection<string> _samplePrompts = new();

    [ObservableProperty]
    private string? _selectedSamplePrompt;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public SettingsViewModel(IConfiguration configuration, ILogger<SettingsViewModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        LoadConfiguration();
        LoadSamplePrompts();
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configuration.GetSection("FilterConfiguration").Get<FilterConfiguration>();
            if (config != null)
            {
                FilterConfig = config;
                SelectedEmailProvider = config.EmailProvider ?? "Gmail";

                LlmTemplates.Clear();
                foreach (var template in config.LlmFilterTemplates)
                {
                    LlmTemplates.Add(template);
                }

                FilterRules.Clear();
                foreach (var rule in config.FilterRules)
                {
                    FilterRules.Add(rule);
                }
            }

            _logger.LogInformation("Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
            MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSamplePrompts()
    {
        SamplePrompts.Clear();
        SamplePrompts.Add("Identify misdirected academic emails");
        SamplePrompts.Add("Detect spam and phishing attempts");
        SamplePrompts.Add("Classify newsletters and digests");
        SamplePrompts.Add("Find technical support requests");
        SamplePrompts.Add("Detect urgent action items");
        SamplePrompts.Add("Identify recruitment emails");
        SamplePrompts.Add("Filter social media notifications");
        SamplePrompts.Add("Detect invoices and billing");
        SamplePrompts.Add("Find out-of-office replies");
        SamplePrompts.Add("Identify marketing emails");
    }

    [RelayCommand]
    private void ApplySamplePrompt()
    {
        if (string.IsNullOrEmpty(SelectedSamplePrompt))
            return;

        // Map sample prompts to template IDs
        var templateMap = new Dictionary<string, string>
        {
            ["Identify misdirected academic emails"] = "academic-mistaken-identity",
            ["Detect spam and phishing attempts"] = "aggressive-spam-detector",
            ["Classify newsletters and digests"] = "newsletter-classifier",
            ["Find technical support requests"] = "technical-support-detector",
            ["Detect urgent action items"] = "urgent-action-required",
            ["Identify recruitment emails"] = "recruitment-detector",
            ["Filter social media notifications"] = "social-media-notification",
            ["Detect invoices and billing"] = "invoice-billing-detector",
            ["Find out-of-office replies"] = "out-of-office-detector",
            ["Identify marketing emails"] = "marketing-promotional"
        };

        if (templateMap.TryGetValue(SelectedSamplePrompt, out var templateId))
        {
            var template = LlmTemplates.FirstOrDefault(t => t.Id == templateId);
            if (template != null)
            {
                MessageBox.Show(
                    $"Template: {template.Name}\n\nDescription: {template.Description}\n\nSystem Prompt:\n{template.SystemPrompt}",
                    "Sample Template Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    [RelayCommand]
    private void AddNewRule()
    {
        var newRule = new FilterRule
        {
            Name = "New Filter Rule",
            Enabled = true,
            ConfidenceThreshold = 0.7f,
            Action = FilterAction.MoveToFolder,
            TargetFolder = "Filtered"
        };

        FilterRules.Add(newRule);
        FilterConfig.FilterRules.Add(newRule);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveRule(FilterRule rule)
    {
        if (rule != null)
        {
            FilterRules.Remove(rule);
            FilterConfig.FilterRules.Remove(rule);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void AddNewTemplate()
    {
        var newTemplate = new LlmFilterTemplate
        {
            Id = $"custom-template-{Guid.NewGuid():N}".Substring(0, 30),
            Name = "New LLM Template",
            Description = "Custom filter template",
            SystemPrompt = "You are an email classifier...",
            PromptTemplate = "Analyze this email:\n\nFrom: {from}\nSubject: {subject}\nBody: {body}",
            Temperature = 0.3f,
            MaxTokens = 400
        };

        LlmTemplates.Add(newTemplate);
        FilterConfig.LlmFilterTemplates.Add(newTemplate);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void RemoveTemplate(LlmFilterTemplate template)
    {
        if (template != null)
        {
            LlmTemplates.Remove(template);
            FilterConfig.LlmFilterTemplates.Remove(template);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        try
        {
            // Update the filter config
            FilterConfig.EmailProvider = SelectedEmailProvider;

            // Read existing appsettings.json
            var json = await File.ReadAllTextAsync(_configPath);
            var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            // Create new configuration object
            var newConfig = new Dictionary<string, object>();

            // Copy all existing properties
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "FilterConfiguration")
                {
                    newConfig[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText()) ?? new object();
                }
            }

            // Add updated FilterConfiguration
            newConfig["FilterConfiguration"] = FilterConfig;

            // Write back to file with pretty formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var updatedJson = JsonSerializer.Serialize(newConfig, options);
            await File.WriteAllTextAsync(_configPath, updatedJson);

            HasUnsavedChanges = false;
            MessageBox.Show("Configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            _logger.LogInformation("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ReloadConfiguration()
    {
        if (HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Are you sure you want to reload?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        LoadConfiguration();
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private void OpenConfigFile()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _configPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening config file");
            MessageBox.Show($"Error opening config file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSelectedEmailProviderChanged(string value)
    {
        if (FilterConfig != null)
        {
            FilterConfig.EmailProvider = value;
            HasUnsavedChanges = true;
        }
    }
}

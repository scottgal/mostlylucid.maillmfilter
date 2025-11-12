using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using MostlyLucid.MailLLMFilter.Core.Services;
using System.Collections.ObjectModel;

namespace MostlyLucid.MailLLMFilter.App.ViewModels;

public partial class TemplateTestViewModel : ObservableObject
{
    private readonly FilterConfiguration _config;
    private readonly ILlmService _llmService;
    private readonly ILogger<TemplateTestViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LlmFilterTemplate> _availableTemplates = new();

    [ObservableProperty]
    private LlmFilterTemplate? _selectedTemplate;

    [ObservableProperty]
    private string _testEmailFrom = "test@example.com";

    [ObservableProperty]
    private string _testEmailSubject = "Test Subject";

    [ObservableProperty]
    private string _testEmailBody = "Test email body...";

    [ObservableProperty]
    private string _testKeywords = "";

    [ObservableProperty]
    private string _testTopics = "";

    [ObservableProperty]
    private string _testMentions = "";

    [ObservableProperty]
    private string _testResult = "";

    [ObservableProperty]
    private bool _isTesting;

    public TemplateTestViewModel(
        IOptions<FilterConfiguration> config,
        ILlmService llmService,
        ILogger<TemplateTestViewModel> logger)
    {
        _config = config.Value;
        _llmService = llmService;
        _logger = logger;

        LoadTemplates();
    }

    private void LoadTemplates()
    {
        AvailableTemplates.Clear();
        foreach (var template in _config.LlmFilterTemplates)
        {
            AvailableTemplates.Add(template);
        }

        if (AvailableTemplates.Any())
        {
            SelectedTemplate = AvailableTemplates.First();
        }
    }

    [RelayCommand]
    private async Task TestTemplateAsync()
    {
        if (SelectedTemplate == null)
        {
            TestResult = "No template selected";
            return;
        }

        try
        {
            IsTesting = true;
            TestResult = "Testing...";

            // Create sample email
            var testEmail = new EmailMessage
            {
                Id = "test-id",
                From = TestEmailFrom,
                Subject = TestEmailSubject,
                Body = TestEmailBody,
                ReceivedDate = DateTime.Now
            };

            // Create sample rule
            var testRule = new FilterRule
            {
                Name = "Test Rule",
                Keywords = TestKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Topics = TestTopics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Mentions = TestMentions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            };

            var result = await _llmService.TestTemplateAsync(testEmail, SelectedTemplate, testRule);

            TestResult = $"Match: {result.IsMatch}\n" +
                        $"Confidence: {result.Confidence:P1}\n" +
                        $"Reason: {result.Reason}\n\n" +
                        $"Detected Topics: {string.Join(", ", result.DetectedTopics)}\n" +
                        $"Detected Mentions: {string.Join(", ", result.DetectedMentions)}\n\n" +
                        $"Full Response:\n{result.FullResponse}";

            _logger.LogInformation("Template test completed: {TemplateId}", SelectedTemplate.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing template");
            TestResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void LoadSampleEmail(string sampleType)
    {
        switch (sampleType)
        {
            case "academic":
                TestEmailFrom = "student@university.edu";
                TestEmailSubject = "Question about CS101 Assignment";
                TestEmailBody = "Hi Prof. Galloway,\n\nI have a question about the assignment due next week for CS101. Can I get an extension?\n\nThanks,\nStudent";
                TestKeywords = "professor, assignment";
                TestTopics = "academic, course";
                TestMentions = "Prof. Galloway";
                break;

            case "spam":
                TestEmailFrom = "noreply@promotions.com";
                TestEmailSubject = "URGENT: You've Won $1,000,000!!!";
                TestEmailBody = "Congratulations! You're our lucky winner! Click here NOW to claim your prize before it expires! Don't miss out on this amazing opportunity!!!";
                TestKeywords = "urgent, click here, won";
                TestTopics = "spam, marketing";
                TestMentions = "";
                break;

            case "newsletter":
                TestEmailFrom = "newsletter@techdigest.com";
                TestEmailSubject = "Weekly Tech Digest - Latest AI News";
                TestEmailBody = "Your weekly roundup of technology news:\n\n1. New AI breakthroughs\n2. Latest gadget reviews\n3. Industry updates\n\nUnsubscribe | Manage Preferences";
                TestKeywords = "newsletter, weekly, digest";
                TestTopics = "newsletter, technology";
                TestMentions = "";
                break;

            case "support":
                TestEmailFrom = "user@company.com";
                TestEmailSubject = "Error 404 when accessing dashboard";
                TestEmailBody = "Hi,\n\nI'm getting a 404 error whenever I try to access my dashboard. The error started this morning. Can you help?\n\nThanks";
                TestKeywords = "error, help";
                TestTopics = "technical, support, bug";
                TestMentions = "";
                break;
        }
    }
}

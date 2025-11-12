using FluentAssertions;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using Xunit;

namespace MostlyLucid.MailLLMFilter.Tests.Configuration;

public class FilterConfigurationTests
{
    [Fact]
    public void FilterConfiguration_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var config = new FilterConfiguration();

        // Assert
        config.Ollama.Should().NotBeNull();
        config.Gmail.Should().NotBeNull();
        config.FilterRules.Should().NotBeNull();
        config.FilterRules.Should().BeEmpty();
        config.AutoReplyTemplates.Should().NotBeNull();
        config.AutoReplyTemplates.Should().BeEmpty();
        config.LlmFilterTemplates.Should().NotBeNull();
        config.LlmFilterTemplates.Should().BeEmpty();
    }

    [Fact]
    public void OllamaSettings_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var settings = new OllamaSettings();

        // Assert
        settings.Endpoint.Should().Be("http://localhost:11434");
        settings.Model.Should().Be("llama3.2");
        settings.Temperature.Should().Be(0.3f);
        settings.MaxTokens.Should().Be(500);
    }

    [Fact]
    public void GmailSettings_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var settings = new GmailSettings();

        // Assert
        settings.CredentialsPath.Should().Be("credentials.json");
        settings.FilteredLabel.Should().Be("Filtered");
        settings.CheckIntervalSeconds.Should().Be(60);
        settings.MaxMessagesPerCheck.Should().Be(50);
    }

    [Fact]
    public void FilterRule_ShouldSupportAllActions()
    {
        // Arrange & Act
        var actions = Enum.GetValues<FilterAction>();

        // Assert
        actions.Should().Contain(FilterAction.MoveToFolder);
        actions.Should().Contain(FilterAction.Delete);
        actions.Should().Contain(FilterAction.MarkAsRead);
        actions.Should().Contain(FilterAction.Archive);
        actions.Should().HaveCount(4);
    }

    [Fact]
    public void FilterRule_WithAllProperties_ShouldWork()
    {
        // Arrange & Act
        var rule = new FilterRule
        {
            Name = "Test Rule",
            Enabled = true,
            Keywords = new List<string> { "keyword1", "keyword2" },
            Topics = new List<string> { "topic1" },
            Mentions = new List<string> { "Prof. Test" },
            ConfidenceThreshold = 0.75f,
            Action = FilterAction.MoveToFolder,
            TargetFolder = "TestFolder",
            AutoReplyTemplateId = "template1",
            CustomPrompt = "Custom LLM prompt",
            LlmFilterTemplateId = "template-id"
        };

        // Assert
        rule.Name.Should().Be("Test Rule");
        rule.Enabled.Should().BeTrue();
        rule.Keywords.Should().HaveCount(2);
        rule.Topics.Should().HaveCount(1);
        rule.Mentions.Should().HaveCount(1);
        rule.ConfidenceThreshold.Should().Be(0.75f);
        rule.Action.Should().Be(FilterAction.MoveToFolder);
        rule.TargetFolder.Should().Be("TestFolder");
        rule.AutoReplyTemplateId.Should().Be("template1");
        rule.CustomPrompt.Should().Be("Custom LLM prompt");
        rule.LlmFilterTemplateId.Should().Be("template-id");
    }

    [Theory]
    [InlineData(0.5f, "Lenient")]
    [InlineData(0.7f, "Balanced")]
    [InlineData(0.9f, "Strict")]
    public void FilterRule_ConfidenceThreshold_ShouldAcceptValidRange(float threshold, string description)
    {
        // Arrange & Act
        var rule = new FilterRule
        {
            ConfidenceThreshold = threshold
        };

        // Assert
        rule.ConfidenceThreshold.Should().Be(threshold);
        rule.ConfidenceThreshold.Should().BeInRange(0.0f, 1.0f);
        description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AutoReplyTemplate_ShouldSupportPlaceholders()
    {
        // Arrange & Act
        var template = new AutoReplyTemplate
        {
            Id = "test-template",
            Name = "Test Template",
            Subject = "Re: {originalSubject}",
            Body = "Hello {sender},\n\nThis is an auto-reply regarding {originalSubject}.",
            IncludeOriginal = true
        };

        // Assert
        template.Id.Should().Be("test-template");
        template.Subject.Should().Contain("{originalSubject}");
        template.Body.Should().Contain("{sender}");
        template.Body.Should().Contain("{originalSubject}");
        template.IncludeOriginal.Should().BeTrue();
    }

    [Fact]
    public void LlmFilterTemplate_WithAllFeatures_ShouldWork()
    {
        // Arrange & Act
        var template = new LlmFilterTemplate
        {
            Id = "advanced-template",
            Name = "Advanced Template",
            Description = "A sophisticated email classifier",
            Model = "llama3.2:70b",
            Temperature = 0.2f,
            MaxTokens = 400,
            SystemPrompt = "You are an expert email classifier",
            PromptTemplate = "Analyze: {subject}\n{body}",
            OutputFormat = "JSON with match, confidence, reason",
            Examples = new List<LlmFilterExample>
            {
                new LlmFilterExample
                {
                    Subject = "Example subject",
                    Body = "Example body",
                    ExpectedResult = "{\"match\": true}",
                    Explanation = "Why this matches"
                }
            },
            RequiresKeywords = true,
            RequiresTopics = true,
            RequiresMentions = false
        };

        // Assert
        template.Id.Should().Be("advanced-template");
        template.Name.Should().Be("Advanced Template");
        template.Description.Should().Be("A sophisticated email classifier");
        template.Model.Should().Be("llama3.2:70b");
        template.Temperature.Should().Be(0.2f);
        template.MaxTokens.Should().Be(400);
        template.SystemPrompt.Should().Contain("expert");
        template.PromptTemplate.Should().Contain("{subject}");
        template.PromptTemplate.Should().Contain("{body}");
        template.OutputFormat.Should().Contain("JSON");
        template.Examples.Should().HaveCount(1);
        template.RequiresKeywords.Should().BeTrue();
        template.RequiresTopics.Should().BeTrue();
        template.RequiresMentions.Should().BeFalse();
    }

    [Fact]
    public void LlmFilterExample_ShouldSupportFewShotLearning()
    {
        // Arrange & Act
        var example = new LlmFilterExample
        {
            Subject = "Question about CS101",
            Body = "Hi Prof. Smith, I have a question about the midterm",
            ExpectedResult = "{\"match\": true, \"confidence\": 0.95, \"reason\": \"Academic email\"}",
            Explanation = "Clear academic context with course mention and professor addressing"
        };

        // Assert
        example.Subject.Should().Be("Question about CS101");
        example.Body.Should().Contain("Prof. Smith");
        example.ExpectedResult.Should().Contain("\"match\": true");
        example.Explanation.Should().Contain("academic");
    }

    [Fact]
    public void LlmFilterTemplate_WithoutOverrides_ShouldUseGlobalSettings()
    {
        // Arrange & Act
        var template = new LlmFilterTemplate
        {
            Id = "simple-template",
            Name = "Simple Template",
            SystemPrompt = "Classify this",
            PromptTemplate = "Email: {body}",
            Model = null,  // Should use global
            Temperature = null,  // Should use global
            MaxTokens = null  // Should use global
        };

        // Assert
        template.Model.Should().BeNull();
        template.Temperature.Should().BeNull();
        template.MaxTokens.Should().BeNull();

        // In actual usage, the service will use:
        // template.Model ?? _settings.Model
        // template.Temperature ?? _settings.Temperature
        // template.MaxTokens ?? _settings.MaxTokens
    }

    [Fact]
    public void FilterConfiguration_WithMultipleRules_ShouldMaintainOrder()
    {
        // Arrange & Act
        var config = new FilterConfiguration
        {
            FilterRules = new List<FilterRule>
            {
                new FilterRule { Name = "First", Enabled = true },
                new FilterRule { Name = "Second", Enabled = true },
                new FilterRule { Name = "Third", Enabled = false }
            }
        };

        // Assert
        config.FilterRules.Should().HaveCount(3);
        config.FilterRules[0].Name.Should().Be("First");
        config.FilterRules[1].Name.Should().Be("Second");
        config.FilterRules[2].Name.Should().Be("Third");

        // Rules are processed in order, first match wins
        var enabledRules = config.FilterRules.Where(r => r.Enabled).ToList();
        enabledRules.Should().HaveCount(2);
    }
}

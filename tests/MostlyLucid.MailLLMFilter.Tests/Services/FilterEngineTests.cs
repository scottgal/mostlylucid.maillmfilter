using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using MostlyLucid.MailLLMFilter.Core.Services;
using Xunit;

namespace MostlyLucid.MailLLMFilter.Tests.Services;

public class FilterEngineTests
{
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IGmailService> _mockGmailService;
    private readonly Mock<ILogger<FilterEngine>> _mockLogger;
    private readonly FilterConfiguration _config;
    private readonly FilterEngine _filterEngine;

    public FilterEngineTests()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockGmailService = new Mock<IGmailService>();
        _mockLogger = new Mock<ILogger<FilterEngine>>();

        _config = new FilterConfiguration
        {
            Ollama = new OllamaSettings
            {
                Endpoint = "http://localhost:11434",
                Model = "llama3.2",
                Temperature = 0.3f,
                MaxTokens = 500
            },
            Gmail = new GmailSettings
            {
                CredentialsPath = "credentials.json",
                FilteredLabel = "Filtered",
                CheckIntervalSeconds = 60,
                MaxMessagesPerCheck = 50
            },
            FilterRules = new List<FilterRule>
            {
                new FilterRule
                {
                    Name = "Test Rule",
                    Enabled = true,
                    Keywords = new List<string> { "test", "important" },
                    Topics = new List<string> { "testing" },
                    Mentions = new List<string> { "Prof. Test" },
                    ConfidenceThreshold = 0.7f,
                    Action = FilterAction.MoveToFolder,
                    TargetFolder = "TestFolder"
                }
            }
        };

        var options = Options.Create(_config);
        _filterEngine = new FilterEngine(options, _mockLlmService.Object, _mockGmailService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task FilterMessageAsync_WithMatchingKeywordsAndHighLlmConfidence_ShouldMatch()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            From = "test@example.com",
            FromName = "Test User",
            Subject = "Important test message",
            Body = "This is a test message",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.9f,
            Reason = "High confidence match",
            FullResponse = "Test response"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.MoveToLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.7f);
        result.MatchedRule.Should().NotBeNull();
        result.MatchedRule!.Name.Should().Be("Test Rule");
        result.ActionTaken.Should().BeTrue();

        _mockGmailService.Verify(x => x.MoveToLabelAsync("123", "TestFolder", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterMessageAsync_WithLowConfidence_ShouldNotMatch()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            From = "test@example.com",
            Subject = "Regular message",
            Body = "This is a regular message",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = false,
            Confidence = 0.2f,
            Reason = "Low confidence",
            FullResponse = "No match"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().BeLessThan(0.7f);
        result.ActionTaken.Should().BeFalse();

        _mockGmailService.Verify(x => x.MoveToLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FilterMessageAsync_WithDisabledRule_ShouldSkipRule()
    {
        // Arrange
        _config.FilterRules[0].Enabled = false;

        var message = new EmailMessage
        {
            Id = "123",
            From = "test@example.com",
            Subject = "Test message",
            Body = "Important test",
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeFalse();

        _mockLlmService.Verify(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FilterMessageAsync_WithMentions_ShouldGiveHigherWeight()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            From = "student@university.edu",
            Subject = "Question for Prof. Test",
            Body = "Hi Prof. Test, I have a question about the exam",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.5f,
            Reason = "Mentions detected",
            FullResponse = "Test response"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.MoveToLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        // Keywords contribute 30%, LLM contributes 70%
        // Mention "Prof. Test" adds 0.4 confidence -> 0.4 * 0.3 = 0.12
        // LLM 0.5 * 0.7 = 0.35
        // Total = 0.47, but with capped keyword at 1.0 it could be higher
        result.Confidence.Should().BeGreaterOrEqualTo(0.4f);
    }

    [Fact]
    public async Task FilterMessageAsync_WithDeleteAction_ShouldDeleteMessage()
    {
        // Arrange
        _config.FilterRules[0].Action = FilterAction.Delete;

        var message = new EmailMessage
        {
            Id = "123",
            From = "spam@example.com",
            Subject = "Important test SPAM",
            Body = "This is spam",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.95f,
            Reason = "Definitely spam",
            FullResponse = "Spam detected"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.ActionTaken.Should().BeTrue();
        result.ActionDescription.Should().Contain("Deleted");

        _mockGmailService.Verify(x => x.DeleteMessageAsync("123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterMessageAsync_WithMarkAsReadAction_ShouldMarkAsRead()
    {
        // Arrange
        _config.FilterRules[0].Action = FilterAction.MarkAsRead;

        var message = new EmailMessage
        {
            Id = "123",
            From = "newsletter@example.com",
            Subject = "Important test newsletter",
            Body = "Weekly digest",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.8f,
            Reason = "Newsletter detected",
            FullResponse = "Newsletter"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.MarkAsReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.ActionDescription.Should().Contain("Marked as read");

        _mockGmailService.Verify(x => x.MarkAsReadAsync("123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterMessageAsync_WithArchiveAction_ShouldArchiveMessage()
    {
        // Arrange
        _config.FilterRules[0].Action = FilterAction.Archive;

        var message = new EmailMessage
        {
            Id = "123",
            From = "old@example.com",
            Subject = "test old email",
            Body = "Archive this",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.75f,
            Reason = "Old email",
            FullResponse = "Archive"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.ArchiveMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.ActionDescription.Should().Contain("Archived");

        _mockGmailService.Verify(x => x.ArchiveMessageAsync("123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterMessageAsync_WithAutoReplyTemplate_ShouldSendAutoReply()
    {
        // Arrange
        _config.FilterRules[0].AutoReplyTemplateId = "test-template";
        _config.AutoReplyTemplates.Add(new AutoReplyTemplate
        {
            Id = "test-template",
            Name = "Test Template",
            Subject = "Re: {originalSubject}",
            Body = "Hello {sender}, this is an auto-reply",
            IncludeOriginal = false
        });

        var message = new EmailMessage
        {
            Id = "123",
            From = "sender@example.com",
            FromName = "Sender Name",
            Subject = "Important test question",
            Body = "I have a question",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.9f,
            Reason = "Match found",
            FullResponse = "Matched"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.MoveToLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockGmailService
            .Setup(x => x.SendReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.ActionDescription.Should().Contain("Auto-reply sent");

        _mockGmailService.Verify(x => x.SendReplyAsync(
            "123",
            It.Is<string>(s => s.Contains("Re: Important test question")),
            It.Is<string>(b => b.Contains("Sender Name") || b.Contains("sender@example.com")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessUnreadMessagesAsync_WithMultipleMessages_ShouldProcessAll()
    {
        // Arrange
        var messages = new List<EmailMessage>
        {
            new EmailMessage { Id = "1", From = "test1@example.com", Subject = "Test 1", Body = "Important test", ReceivedDate = DateTime.UtcNow },
            new EmailMessage { Id = "2", From = "test2@example.com", Subject = "Test 2", Body = "Another test", ReceivedDate = DateTime.UtcNow },
            new EmailMessage { Id = "3", From = "test3@example.com", Subject = "Regular", Body = "Regular email", ReceivedDate = DateTime.UtcNow }
        };

        _mockGmailService.SetupGet(x => x.IsAuthenticated).Returns(true);
        _mockGmailService
            .Setup(x => x.GetUnreadMessagesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmAnalysisResult { IsMatch = false, Confidence = 0.3f, Reason = "No match", FullResponse = "" });

        // Act
        var results = await _filterEngine.ProcessUnreadMessagesAsync();

        // Assert
        results.Should().HaveCount(3);
        _mockLlmService.Verify(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessUnreadMessagesAsync_WhenNotAuthenticated_ShouldReturnEmpty()
    {
        // Arrange
        _mockGmailService.SetupGet(x => x.IsAuthenticated).Returns(false);

        // Act
        var results = await _filterEngine.ProcessUnreadMessagesAsync();

        // Assert
        results.Should().BeEmpty();
        _mockGmailService.Verify(x => x.GetUnreadMessagesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FilterMessageAsync_WithMultipleRules_ShouldStopAtFirstMatch()
    {
        // Arrange
        _config.FilterRules.Add(new FilterRule
        {
            Name = "Second Rule",
            Enabled = true,
            Keywords = new List<string> { "second" },
            Topics = new List<string>(),
            Mentions = new List<string>(),
            ConfidenceThreshold = 0.5f,
            Action = FilterAction.Archive,
            TargetFolder = null
        });

        var message = new EmailMessage
        {
            Id = "123",
            From = "test@example.com",
            Subject = "Important test message",
            Body = "This matches the first rule",
            ReceivedDate = DateTime.UtcNow
        };

        var llmResult = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.9f,
            Reason = "Match",
            FullResponse = "Matched"
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        _mockGmailService
            .Setup(x => x.MoveToLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeTrue();
        result.MatchedRule!.Name.Should().Be("Test Rule"); // First rule matched

        // LLM should only be called once for the first rule
        _mockLlmService.Verify(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterMessageAsync_WithException_ShouldHandleGracefully()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            From = "test@example.com",
            Subject = "Test",
            Body = "Test",
            ReceivedDate = DateTime.UtcNow
        };

        _mockLlmService
            .Setup(x => x.AnalyzeEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<FilterRule>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service error"));

        // Act
        var result = await _filterEngine.FilterMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.IsMatch.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("LLM service error");
    }
}

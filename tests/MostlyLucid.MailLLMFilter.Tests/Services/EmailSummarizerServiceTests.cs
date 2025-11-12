using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using MostlyLucid.MailLLMFilter.Core.Services;
using Xunit;

namespace MostlyLucid.MailLLMFilter.Tests.Services;

public class EmailSummarizerServiceTests
{
    private readonly Mock<ILogger<EmailSummarizerService>> _mockLogger;
    private readonly FilterConfiguration _config;
    private readonly EmailSummarizerService _summarizer;

    public EmailSummarizerServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailSummarizerService>>();

        _config = new FilterConfiguration
        {
            Ollama = new OllamaSettings
            {
                Endpoint = "http://localhost:11434",
                Model = "llama3.2",
                Temperature = 0.3f,
                MaxTokens = 500
            }
        };

        var options = Options.Create(_config);
        _summarizer = new EmailSummarizerService(options, _mockLogger.Object);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithShortEmail_ShouldNotSummarize()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            Subject = "Short email",
            Body = "This is a short email that doesn't need summarization.",
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.Should().NotBeNull();
        summary.WasSummarized.Should().BeFalse();
        summary.Body.Should().Be(message.Body);
        summary.OriginalMessage.Should().Be(message);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithLongEmail_ShouldSummarize()
    {
        // Arrange
        var longBody = new string('a', 5000) + " This is an important sentence. Another important detail.";

        var message = new EmailMessage
        {
            Id = "123",
            Subject = "Long email",
            Body = longBody,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.Should().NotBeNull();
        summary.WasSummarized.Should().BeTrue();
        summary.Body.Length.Should().BeLessThanOrEqualTo(1000);
        summary.OriginalMessage.Should().Be(message);
        summary.SummaryMetadata.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithNullBody_ShouldHandleGracefully()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            Subject = "No body",
            Body = null,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message);

        // Assert
        summary.Should().NotBeNull();
        summary.WasSummarized.Should().BeFalse();
        summary.Body.Should().BeEmpty();
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithEmptyBody_ShouldHandleGracefully()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            Subject = "Empty body",
            Body = string.Empty,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message);

        // Assert
        summary.Should().NotBeNull();
        summary.WasSummarized.Should().BeFalse();
        summary.Body.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("Hello", 1)]
    [InlineData("Hello world", 2)]
    [InlineData("This is a test", 3)]
    [InlineData("Four score and seven years ago", 6)]
    public void EstimateTokenCount_ShouldCalculateCorrectly(string text, int expectedTokens)
    {
        // Act
        var tokenCount = _summarizer.EstimateTokenCount(text);

        // Assert
        // Using ~4 chars per token, so allow some variance
        tokenCount.Should().BeGreaterThanOrEqualTo(expectedTokens - 1);
        tokenCount.Should().BeLessThanOrEqualTo(expectedTokens + 1);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_ShouldIncludeEstimatedTokens()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            Subject = "Test",
            Body = "This is a test email with some content.",
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message);

        // Assert
        summary.EstimatedTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithExactMaxLength_ShouldNotSummarize()
    {
        // Arrange
        var body = new string('a', 1000);
        var message = new EmailMessage
        {
            Id = "123",
            Body = body,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.WasSummarized.Should().BeFalse();
        summary.Body.Should().Be(body);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithSlightlyOverMaxLength_ShouldSummarize()
    {
        // Arrange
        var body = new string('a', 1001);
        var message = new EmailMessage
        {
            Id = "123",
            Body = body,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.WasSummarized.Should().BeTrue();
        summary.Body.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_ShouldPreserveOriginalMessage()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = "123",
            From = "sender@example.com",
            Subject = "Test",
            Body = new string('a', 5000),
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.OriginalMessage.Should().Be(message);
        summary.OriginalMessage.Body.Should().HaveLength(5000); // Original unchanged
        summary.Body.Length.Should().BeLessThanOrEqualTo(1000); // Summary is shorter
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithMultipleSentences_ShouldExtractKey()
    {
        // Arrange
        var body = @"This is the first sentence. This is the second sentence. This is the third sentence.
                     This is an important question? This is the fifth sentence. This is the sixth sentence.
                     This contains an urgent matter. This is the eighth sentence." + new string('x', 3000);

        var message = new EmailMessage
        {
            Id = "123",
            Body = body,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 500);

        // Assert
        summary.WasSummarized.Should().BeTrue();
        summary.Body.Length.Should().BeLessThanOrEqualTo(500);

        // Should prefer important sentences (questions, urgent keywords)
        // This is a heuristic test, may not always pass depending on implementation
        var lowerSummary = summary.Body.ToLower();
        (lowerSummary.Contains("question") || lowerSummary.Contains("urgent") || lowerSummary.Contains("first")).Should().BeTrue();
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithDifferentMaxLengths_ShouldRespectLimit()
    {
        // Arrange
        var longBody = new string('a', 10000);
        var message = new EmailMessage
        {
            Id = "123",
            Body = longBody,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary500 = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 500);
        var summary1000 = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);
        var summary2000 = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 2000);

        // Assert
        summary500.Body.Length.Should().BeLessThanOrEqualTo(500);
        summary1000.Body.Length.Should().BeLessThanOrEqualTo(1000);
        summary2000.Body.Length.Should().BeLessThanOrEqualTo(2000);

        summary500.Body.Length.Should().BeLessThan(summary1000.Body.Length);
        summary1000.Body.Length.Should().BeLessThan(summary2000.Body.Length);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_ShouldIncludeSummaryMetadata()
    {
        // Arrange
        var longBody = new string('a', 5000);
        var message = new EmailMessage
        {
            Id = "123",
            Body = longBody,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 1000);

        // Assert
        summary.SummaryMetadata.Should().NotBeNullOrEmpty();
        summary.SummaryMetadata.Should().Contain("5000");
        summary.SummaryMetadata.Should().MatchRegex(@"\d+.*→.*\d+"); // Should show "X → Y chars" pattern
    }

    [Fact]
    public void EstimateTokenCount_WithVeryLongText_ShouldScale()
    {
        // Arrange
        var longText = new string('a', 10000);

        // Act
        var tokenCount = _summarizer.EstimateTokenCount(longText);

        // Assert
        // ~4 chars per token, so 10000 chars ≈ 2500 tokens
        tokenCount.Should().BeInRange(2000, 3000);
    }

    [Fact]
    public async Task SummarizeIfNeededAsync_WithStructuredEmail_ShouldPreserveKeyInformation()
    {
        // Arrange
        var structuredBody = @"
Dear Recipient,

This is the introduction paragraph with some background information that is not critical.

The main point of this email is to inform you about an important deadline. The project submission is due on Friday.

There are several action items:
1. Review the documentation
2. Complete the testing
3. Submit the final report

Please let me know if you have any questions?

Best regards,
Sender
" + new string('x', 3000); // Add padding to exceed limit

        var message = new EmailMessage
        {
            Id = "123",
            Body = structuredBody,
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var summary = await _summarizer.SummarizeIfNeededAsync(message, maxLength: 500);

        // Assert
        summary.WasSummarized.Should().BeTrue();
        var lowerSummary = summary.Body.ToLower();

        // Should preserve key information (this is heuristic)
        (lowerSummary.Contains("deadline") || lowerSummary.Contains("friday") || lowerSummary.Contains("important")).Should().BeTrue();
    }
}

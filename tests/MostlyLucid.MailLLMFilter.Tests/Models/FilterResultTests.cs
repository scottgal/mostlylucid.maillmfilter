using FluentAssertions;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using Xunit;

namespace MostlyLucid.MailLLMFilter.Tests.Models;

public class FilterResultTests
{
    [Fact]
    public void FilterResult_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var result = new FilterResult();

        // Assert
        result.Message.Should().BeNull();
        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.MatchedRule.Should().BeNull();
        result.Reason.Should().BeNull();
        result.ActionTaken.Should().BeFalse();
        result.ActionDescription.Should().BeNull();
        result.Error.Should().BeNull();
        result.LlmAnalysis.Should().BeNull();
        result.FilteredAt.Should().Be(default);
    }

    [Fact]
    public void FilterResult_WithMatch_ShouldHaveAllFields()
    {
        // Arrange
        var message = new EmailMessage { Id = "123", Subject = "Test" };
        var rule = new FilterRule { Name = "Test Rule", Action = FilterAction.MoveToFolder };
        var filteredAt = DateTime.UtcNow;

        // Act
        var result = new FilterResult
        {
            Message = message,
            IsMatch = true,
            Confidence = 0.85f,
            MatchedRule = rule,
            Reason = "Matched because of keywords and LLM analysis",
            ActionTaken = true,
            ActionDescription = "Moved to folder: TestFolder",
            LlmAnalysis = "Full LLM response text",
            FilteredAt = filteredAt
        };

        // Assert
        result.Message.Should().Be(message);
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().Be(0.85f);
        result.MatchedRule.Should().Be(rule);
        result.Reason.Should().Contain("keywords");
        result.ActionTaken.Should().BeTrue();
        result.ActionDescription.Should().Contain("Moved to folder");
        result.LlmAnalysis.Should().Be("Full LLM response text");
        result.FilteredAt.Should().Be(filteredAt);
    }

    [Fact]
    public void FilterResult_WithError_ShouldCaptureError()
    {
        // Arrange & Act
        var result = new FilterResult
        {
            IsMatch = false,
            Error = "LLM service unavailable"
        };

        // Assert
        result.IsMatch.Should().BeFalse();
        result.Error.Should().Be("LLM service unavailable");
        result.ActionTaken.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.0f, "Very low")]
    [InlineData(0.5f, "Medium")]
    [InlineData(0.7f, "Threshold")]
    [InlineData(0.9f, "High")]
    [InlineData(1.0f, "Maximum")]
    public void FilterResult_ConfidenceRange_ShouldBeValid(float confidence, string description)
    {
        // Arrange & Act
        var result = new FilterResult
        {
            Confidence = confidence
        };

        // Assert
        result.Confidence.Should().Be(confidence);
        result.Confidence.Should().BeInRange(0.0f, 1.0f);
        description.Should().NotBeNullOrEmpty();
    }
}

public class LlmAnalysisResultTests
{
    [Fact]
    public void LlmAnalysisResult_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var result = new LlmAnalysisResult();

        // Assert
        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.Reason.Should().BeNull();
        result.FullResponse.Should().BeNull();
        result.DetectedTopics.Should().BeNull();
        result.DetectedMentions.Should().BeNull();
    }

    [Fact]
    public void LlmAnalysisResult_WithFullData_ShouldStoreAllFields()
    {
        // Arrange & Act
        var result = new LlmAnalysisResult
        {
            IsMatch = true,
            Confidence = 0.92f,
            Reason = "Email clearly mentions Prof. Galloway and discusses course CS101",
            FullResponse = "Full JSON response from LLM",
            DetectedTopics = new List<string> { "academic", "university", "course" },
            DetectedMentions = new List<string> { "Prof. Galloway", "CS101" }
        };

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().Be(0.92f);
        result.Reason.Should().Contain("Prof. Galloway");
        result.FullResponse.Should().Be("Full JSON response from LLM");
        result.DetectedTopics.Should().HaveCount(3);
        result.DetectedTopics.Should().Contain("academic");
        result.DetectedMentions.Should().HaveCount(2);
        result.DetectedMentions.Should().Contain("Prof. Galloway");
    }

    [Fact]
    public void LlmAnalysisResult_WithEmptyCollections_ShouldHandleGracefully()
    {
        // Arrange & Act
        var result = new LlmAnalysisResult
        {
            IsMatch = false,
            Confidence = 0.1f,
            Reason = "No match found",
            DetectedTopics = new List<string>(),
            DetectedMentions = new List<string>()
        };

        // Assert
        result.DetectedTopics.Should().BeEmpty();
        result.DetectedMentions.Should().BeEmpty();
    }
}

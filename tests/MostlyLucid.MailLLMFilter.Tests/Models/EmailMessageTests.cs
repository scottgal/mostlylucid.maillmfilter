using FluentAssertions;
using MostlyLucid.MailLLMFilter.Core.Models;
using Xunit;

namespace MostlyLucid.MailLLMFilter.Tests.Models;

public class EmailMessageTests
{
    [Fact]
    public void EmailMessage_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var message = new EmailMessage();

        // Assert
        message.Id.Should().BeNull();
        message.ThreadId.Should().BeNull();
        message.From.Should().BeNull();
        message.FromName.Should().BeNull();
        message.To.Should().BeNull();
        message.Subject.Should().BeNull();
        message.Body.Should().BeNull();
        message.Snippet.Should().BeNull();
        message.Labels.Should().BeNull();
        message.ReceivedDate.Should().Be(default);
        message.IsUnread.Should().BeFalse();
    }

    [Fact]
    public void EmailMessage_ShouldAcceptAllProperties()
    {
        // Arrange
        var receivedDate = DateTime.UtcNow;
        var labels = new List<string> { "INBOX", "UNREAD" };

        // Act
        var message = new EmailMessage
        {
            Id = "msg123",
            ThreadId = "thread456",
            From = "sender@example.com",
            FromName = "Sender Name",
            To = "recipient@example.com",
            Subject = "Test Subject",
            Body = "Test Body Content",
            Snippet = "Test snippet...",
            ReceivedDate = receivedDate,
            Labels = labels,
            IsUnread = true
        };

        // Assert
        message.Id.Should().Be("msg123");
        message.ThreadId.Should().Be("thread456");
        message.From.Should().Be("sender@example.com");
        message.FromName.Should().Be("Sender Name");
        message.To.Should().Be("recipient@example.com");
        message.Subject.Should().Be("Test Subject");
        message.Body.Should().Be("Test Body Content");
        message.Snippet.Should().Be("Test snippet...");
        message.ReceivedDate.Should().Be(receivedDate);
        message.Labels.Should().BeEquivalentTo(labels);
        message.IsUnread.Should().BeTrue();
    }

    [Theory]
    [InlineData("sender@example.com", "Sender Name", "Sender Name")]
    [InlineData("sender@example.com", null, "sender@example.com")]
    [InlineData("sender@example.com", "", "sender@example.com")]
    public void EmailMessage_FromName_FallbackBehavior(string from, string fromName, string expected)
    {
        // Arrange & Act
        var message = new EmailMessage
        {
            From = from,
            FromName = fromName
        };

        // Assert
        // In the actual usage, the code uses FromName ?? From for display
        var displayName = message.FromName ?? message.From;
        displayName.Should().Be(expected);
    }

    [Fact]
    public void EmailMessage_WithLongBody_ShouldStoreFullContent()
    {
        // Arrange
        var longBody = new string('a', 10000);

        // Act
        var message = new EmailMessage
        {
            Id = "123",
            Body = longBody
        };

        // Assert
        message.Body.Should().HaveLength(10000);
        message.Body.Should().Be(longBody);
    }

    [Fact]
    public void EmailMessage_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var message = new EmailMessage
        {
            Subject = "Test with émojis 😀 and spëcial çharactérs",
            Body = "Body with <html> tags and & special chars\nNew lines\tTabs"
        };

        // Assert
        message.Subject.Should().Contain("émojis");
        message.Subject.Should().Contain("😀");
        message.Body.Should().Contain("<html>");
        message.Body.Should().Contain("&");
        message.Body.Should().Contain("\n");
        message.Body.Should().Contain("\t");
    }
}

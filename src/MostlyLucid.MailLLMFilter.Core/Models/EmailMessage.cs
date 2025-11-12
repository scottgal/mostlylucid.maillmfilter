namespace MostlyLucid.MailLLMFilter.Core.Models;

/// <summary>
/// Represents an email message
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Unique message ID from Gmail
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Thread ID from Gmail
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Sender email address
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Sender name (if available)
    /// </summary>
    public string? FromName { get; set; }

    /// <summary>
    /// Recipient email addresses
    /// </summary>
    public List<string> To { get; set; } = new();

    /// <summary>
    /// Email subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Email body (plain text or HTML)
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When the email was received
    /// </summary>
    public DateTime ReceivedDate { get; set; }

    /// <summary>
    /// Gmail labels applied to this message
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Whether the message is unread
    /// </summary>
    public bool IsUnread { get; set; }

    /// <summary>
    /// Snippet/preview text
    /// </summary>
    public string? Snippet { get; set; }
}

using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Service for interacting with Gmail API
/// </summary>
public interface IGmailService
{
    /// <summary>
    /// Initialize and authenticate with Gmail
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unread messages
    /// </summary>
    Task<List<EmailMessage>> GetUnreadMessagesAsync(int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move a message to a label/folder
    /// </summary>
    Task MoveToLabelAsync(string messageId, string labelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a message
    /// </summary>
    Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a message as read
    /// </summary>
    Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a message
    /// </summary>
    Task ArchiveMessageAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an email reply
    /// </summary>
    Task SendReplyAsync(string messageId, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if service is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
}

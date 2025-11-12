using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Engine for filtering email messages
/// </summary>
public interface IFilterEngine
{
    /// <summary>
    /// Process and filter a single email message
    /// </summary>
    Task<FilterResult> FilterMessageAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process all unread messages
    /// </summary>
    Task<List<FilterResult>> ProcessUnreadMessagesAsync(CancellationToken cancellationToken = default);
}

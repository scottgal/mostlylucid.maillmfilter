using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Service for summarizing long emails to fit within LLM context windows
/// </summary>
public interface IEmailSummarizerService
{
    /// <summary>
    /// Summarizes an email if it exceeds the maximum length
    /// </summary>
    /// <param name="message">The email message to summarize</param>
    /// <param name="maxLength">Maximum character length before summarization is needed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A summary of the email if it was too long, otherwise the original message</returns>
    Task<EmailSummary> SummarizeIfNeededAsync(EmailMessage message, int maxLength = 3000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the token count estimate for an email body
    /// </summary>
    /// <param name="text">The text to estimate tokens for</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokenCount(string text);
}

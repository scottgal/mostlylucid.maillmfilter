namespace MostlyLucid.MailLLMFilter.Core.Models;

/// <summary>
/// Represents a summarized email for LLM analysis
/// </summary>
public class EmailSummary
{
    /// <summary>
    /// The original email message
    /// </summary>
    public EmailMessage OriginalMessage { get; set; } = new();

    /// <summary>
    /// Whether the email was summarized
    /// </summary>
    public bool WasSummarized { get; set; }

    /// <summary>
    /// The summarized body (or original if not summarized)
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Summary metadata (e.g., "Summarized from 5000 chars to 1000 chars")
    /// </summary>
    public string SummaryMetadata { get; set; } = string.Empty;

    /// <summary>
    /// Estimated token count of the summary
    /// </summary>
    public int EstimatedTokens { get; set; }

    /// <summary>
    /// Key points extracted from the email (if available)
    /// </summary>
    public List<string> KeyPoints { get; set; } = new();
}

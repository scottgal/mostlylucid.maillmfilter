using MostlyLucid.MailLLMFilter.Core.Configuration;

namespace MostlyLucid.MailLLMFilter.Core.Models;

/// <summary>
/// Result of filtering an email message
/// </summary>
public class FilterResult
{
    /// <summary>
    /// The email message that was filtered
    /// </summary>
    public required EmailMessage Message { get; set; }

    /// <summary>
    /// Whether the message matched any filter rules
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// The rule that matched (if any)
    /// </summary>
    public FilterRule? MatchedRule { get; set; }

    /// <summary>
    /// Reason for the match
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// LLM analysis of the message
    /// </summary>
    public string? LlmAnalysis { get; set; }

    /// <summary>
    /// Whether action was taken
    /// </summary>
    public bool ActionTaken { get; set; }

    /// <summary>
    /// Description of action taken
    /// </summary>
    public string? ActionDescription { get; set; }

    /// <summary>
    /// Any errors that occurred
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the filtering was performed
    /// </summary>
    public DateTime FilteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// LLM analysis result for an email
/// </summary>
public class LlmAnalysisResult
{
    /// <summary>
    /// Whether the message matches the filter criteria
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Confidence level (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Reason for the decision
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Full LLM response text
    /// </summary>
    public string FullResponse { get; set; } = string.Empty;

    /// <summary>
    /// Detected topics
    /// </summary>
    public List<string> DetectedTopics { get; set; } = new();

    /// <summary>
    /// Detected mentions
    /// </summary>
    public List<string> DetectedMentions { get; set; } = new();
}

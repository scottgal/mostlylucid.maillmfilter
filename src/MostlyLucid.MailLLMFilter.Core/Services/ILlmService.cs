using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Service for interacting with LLM (Ollama)
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Analyze an email message against a filter rule
    /// </summary>
    Task<LlmAnalysisResult> AnalyzeEmailAsync(EmailMessage message, FilterRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the LLM service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

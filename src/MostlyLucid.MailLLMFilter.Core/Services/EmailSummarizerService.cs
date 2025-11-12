using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using OllamaSharp;
using System.Text;
using System.Text.RegularExpressions;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Service for summarizing long emails using LLM
/// </summary>
public class EmailSummarizerService : IEmailSummarizerService
{
    private readonly FilterConfiguration _config;
    private readonly OllamaSettings _settings;
    private readonly ILogger<EmailSummarizerService> _logger;
    private readonly OllamaApiClient _client;

    // Approximate token estimation: ~4 chars per token for English text
    private const int CHARS_PER_TOKEN = 4;

    public EmailSummarizerService(
        IOptions<FilterConfiguration> config,
        ILogger<EmailSummarizerService> logger)
    {
        _config = config.Value;
        _settings = config.Value.Ollama;
        _logger = logger;
        _client = new OllamaApiClient(_settings.Endpoint, _settings.Model);
    }

    public async Task<EmailSummary> SummarizeIfNeededAsync(
        EmailMessage message,
        int maxLength = 3000,
        CancellationToken cancellationToken = default)
    {
        var summary = new EmailSummary
        {
            OriginalMessage = message,
            Body = message.Body ?? string.Empty
        };

        // If body is within limits, no summarization needed
        if (string.IsNullOrEmpty(message.Body) || message.Body.Length <= maxLength)
        {
            summary.WasSummarized = false;
            summary.EstimatedTokens = EstimateTokenCount(message.Body ?? string.Empty);
            return summary;
        }

        try
        {
            _logger.LogInformation(
                "Email body length ({Length} chars) exceeds max ({MaxLength}). Summarizing...",
                message.Body.Length,
                maxLength);

            // Try extractive summarization first (faster)
            var extractiveSummary = PerformExtractiveSummarization(message.Body, maxLength);

            // If extractive summary is good enough, use it
            if (extractiveSummary.Length <= maxLength)
            {
                summary.Body = extractiveSummary;
                summary.WasSummarized = true;
                summary.SummaryMetadata = $"Extractive summary: {message.Body.Length} → {extractiveSummary.Length} chars";
                summary.EstimatedTokens = EstimateTokenCount(extractiveSummary);

                _logger.LogDebug("Used extractive summarization");
                return summary;
            }

            // If still too long, use LLM-based abstractive summarization
            var abstractiveSummary = await PerformAbstractiveSummarizationAsync(
                message.Body,
                maxLength,
                cancellationToken);

            summary.Body = abstractiveSummary;
            summary.WasSummarized = true;
            summary.SummaryMetadata = $"LLM summary: {message.Body.Length} → {abstractiveSummary.Length} chars";
            summary.EstimatedTokens = EstimateTokenCount(abstractiveSummary);

            _logger.LogInformation("Email summarized successfully using LLM");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to summarize email, truncating instead");

            // Fallback: simple truncation
            summary.Body = TruncateIntelligently(message.Body, maxLength);
            summary.WasSummarized = true;
            summary.SummaryMetadata = $"Truncated: {message.Body.Length} → {summary.Body.Length} chars";
            summary.EstimatedTokens = EstimateTokenCount(summary.Body);
        }

        return summary;
    }

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimation: ~4 characters per token for English
        // This is approximate; actual tokenization varies by model
        return text.Length / CHARS_PER_TOKEN;
    }

    /// <summary>
    /// Performs extractive summarization by selecting important sentences
    /// </summary>
    private string PerformExtractiveSummarization(string text, int targetLength)
    {
        // Split into sentences
        var sentences = SplitIntoSentences(text);

        if (sentences.Count == 0)
            return TruncateIntelligently(text, targetLength);

        // Score sentences based on importance
        var scoredSentences = sentences
            .Select((sentence, index) => new
            {
                Sentence = sentence,
                Index = index,
                Score = ScoreSentenceImportance(sentence, index, sentences.Count)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        // Build summary by selecting top sentences
        var summary = new StringBuilder();
        var addedIndices = new HashSet<int>();

        foreach (var item in scoredSentences)
        {
            var potentialLength = summary.Length + item.Sentence.Length + 1;

            if (potentialLength > targetLength && summary.Length > 0)
                break;

            addedIndices.Add(item.Index);
        }

        // Rebuild in original order
        var result = new StringBuilder();
        for (int i = 0; i < sentences.Count; i++)
        {
            if (addedIndices.Contains(i))
            {
                if (result.Length > 0)
                    result.Append(" ");
                result.Append(sentences[i]);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Performs LLM-based abstractive summarization
    /// </summary>
    private async Task<string> PerformAbstractiveSummarizationAsync(
        string text,
        int targetLength,
        CancellationToken cancellationToken)
    {
        var targetWords = targetLength / 5; // Rough estimate: ~5 chars per word

        var prompt = $@"Summarize the following email content in approximately {targetWords} words or less.
Focus on the key points, main message, and any action items or important details.
Be concise but preserve the essential meaning.

EMAIL CONTENT:
{text.Substring(0, Math.Min(text.Length, 8000))}

SUMMARY:";

        var response = await _client.Generate(new GenerateRequest
        {
            Prompt = prompt,
            Model = _settings.Model,
            Options = new OllamaSharp.Models.RequestOptions
            {
                Temperature = 0.3f, // Low temperature for consistent summaries
                NumPredict = targetLength / 4  // Conservative token limit
            }
        }, cancellationToken);

        var summary = response?.Response?.Trim() ?? string.Empty;

        // If LLM summary is still too long, truncate it
        if (summary.Length > targetLength)
        {
            summary = TruncateIntelligently(summary, targetLength);
        }

        return summary;
    }

    /// <summary>
    /// Splits text into sentences using simple heuristics
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting (can be improved with NLP libraries)
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    /// <summary>
    /// Scores a sentence based on importance heuristics
    /// </summary>
    private double ScoreSentenceImportance(string sentence, int index, int totalSentences)
    {
        var score = 0.0;

        // First and last sentences are often important
        if (index == 0)
            score += 2.0;
        if (index == totalSentences - 1)
            score += 1.5;

        // Sentences with question marks are often important
        if (sentence.Contains('?'))
            score += 1.0;

        // Sentences with certain keywords are more important
        var importantKeywords = new[] {
            "urgent", "important", "deadline", "please", "request",
            "question", "help", "issue", "problem", "thank you",
            "meeting", "call", "appointment", "action", "required"
        };

        foreach (var keyword in importantKeywords)
        {
            if (sentence.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 0.5;
        }

        // Longer sentences (within reason) may contain more information
        var wordCount = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount >= 5 && wordCount <= 30)
            score += 0.3;

        // Sentences with proper capitalization are often more important
        if (char.IsUpper(sentence[0]))
            score += 0.2;

        return score;
    }

    /// <summary>
    /// Intelligently truncates text at sentence or paragraph boundaries
    /// </summary>
    private string TruncateIntelligently(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Try to truncate at paragraph boundary
        var truncatePoint = text.LastIndexOf("\n\n", maxLength, StringComparison.Ordinal);

        // If no paragraph break, try sentence boundary
        if (truncatePoint <= 0)
        {
            truncatePoint = text.LastIndexOfAny(new[] { '.', '!', '?' }, Math.Min(maxLength, text.Length - 1));
        }

        // If no sentence boundary, try word boundary
        if (truncatePoint <= 0)
        {
            truncatePoint = text.LastIndexOf(' ', Math.Min(maxLength, text.Length - 1));
        }

        // If all else fails, hard truncate
        if (truncatePoint <= 0)
        {
            truncatePoint = maxLength;
        }

        return text.Substring(0, truncatePoint) + "... [truncated]";
    }
}

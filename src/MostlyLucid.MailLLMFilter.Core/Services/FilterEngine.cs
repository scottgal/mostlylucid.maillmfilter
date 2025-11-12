using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Implementation of the filter engine
/// </summary>
public class FilterEngine : IFilterEngine
{
    private readonly FilterConfiguration _config;
    private readonly ILlmService _llmService;
    private readonly IEmailService _emailService;
    private readonly ILogger<FilterEngine> _logger;

    public FilterEngine(
        IOptions<FilterConfiguration> config,
        ILlmService llmService,
        IEmailService emailService,
        ILogger<FilterEngine> logger)
    {
        _config = config.Value;
        _llmService = llmService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<FilterResult> FilterMessageAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var result = new FilterResult
        {
            Message = message,
            IsMatch = false,
            Confidence = 0,
            FilteredAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Filtering message {MessageId} from {From}", message.Id, message.From);

            // Check each enabled filter rule
            foreach (var rule in _config.FilterRules.Where(r => r.Enabled))
            {
                _logger.LogDebug("Checking rule: {RuleName}", rule.Name);

                // First, check simple keyword matching
                var keywordMatch = CheckKeywordMatch(message, rule);

                // Then use LLM for more sophisticated analysis
                var llmResult = await _llmService.AnalyzeEmailAsync(message, rule, cancellationToken);

                // Combine results
                var combinedConfidence = (keywordMatch.Confidence * 0.3f) + (llmResult.Confidence * 0.7f);

                if (combinedConfidence >= rule.ConfidenceThreshold)
                {
                    result.IsMatch = true;
                    result.Confidence = combinedConfidence;
                    result.MatchedRule = rule;
                    result.LlmAnalysis = llmResult.FullResponse;
                    result.Reason = $"Keyword match: {keywordMatch.Reason}. LLM analysis: {llmResult.Reason}";

                    _logger.LogInformation(
                        "Message {MessageId} matched rule {RuleName} with confidence {Confidence:F2}",
                        message.Id, rule.Name, combinedConfidence);

                    // Take action on the message
                    await TakeActionAsync(message, rule, result, cancellationToken);

                    // Only process the first matching rule
                    break;
                }
            }

            if (!result.IsMatch)
            {
                _logger.LogInformation("Message {MessageId} did not match any rules", message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering message {MessageId}", message.Id);
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<List<FilterResult>> ProcessUnreadMessagesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<FilterResult>();

        try
        {
            if (!_emailService.IsAuthenticated)
            {
                _logger.LogWarning("Gmail service not authenticated");
                return results;
            }

            var messages = await _emailService.GetUnreadMessagesAsync(
                _config.Gmail.MaxMessagesPerCheck,
                cancellationToken);

            _logger.LogInformation("Processing {Count} unread messages", messages.Count);

            foreach (var message in messages)
            {
                var result = await FilterMessageAsync(message, cancellationToken);
                results.Add(result);

                // Small delay to avoid rate limiting
                await Task.Delay(100, cancellationToken);
            }

            _logger.LogInformation(
                "Processed {Total} messages: {Matched} matched, {Unmatched} unmatched",
                results.Count,
                results.Count(r => r.IsMatch),
                results.Count(r => !r.IsMatch));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing unread messages");
        }

        return results;
    }

    private (float Confidence, string Reason) CheckKeywordMatch(EmailMessage message, FilterRule rule)
    {
        var matches = new List<string>();
        var confidence = 0f;

        // Check keywords in subject and body
        foreach (var keyword in rule.Keywords)
        {
            var keywordLower = keyword.ToLowerInvariant();
            if (message.Subject.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                message.Body.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add($"keyword '{keyword}'");
                confidence += 0.3f;
            }
        }

        // Check mentions
        foreach (var mention in rule.Mentions)
        {
            if (message.Subject.Contains(mention, StringComparison.OrdinalIgnoreCase) ||
                message.Body.Contains(mention, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add($"mention of '{mention}'");
                confidence += 0.4f;
            }
        }

        // Cap confidence at 1.0
        confidence = Math.Min(confidence, 1.0f);

        var reason = matches.Any()
            ? $"Found {string.Join(", ", matches)}"
            : "No keyword matches";

        return (confidence, reason);
    }

    private async Task TakeActionAsync(
        EmailMessage message,
        FilterRule rule,
        FilterResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (rule.Action)
            {
                case FilterAction.MoveToFolder:
                    var targetFolder = rule.TargetFolder ?? _config.Gmail.FilteredLabel ?? "Filtered";
                    await _emailService.MoveToFolderAsync(message.Id, targetFolder, cancellationToken);
                    result.ActionTaken = true;
                    result.ActionDescription = $"Moved to folder: {targetFolder}";
                    break;

                case FilterAction.Delete:
                    await _emailService.DeleteMessageAsync(message.Id, cancellationToken);
                    result.ActionTaken = true;
                    result.ActionDescription = "Deleted message";
                    break;

                case FilterAction.MarkAsRead:
                    await _emailService.MarkAsReadAsync(message.Id, cancellationToken);
                    result.ActionTaken = true;
                    result.ActionDescription = "Marked as read";
                    break;

                case FilterAction.Archive:
                    await _emailService.ArchiveMessageAsync(message.Id, cancellationToken);
                    result.ActionTaken = true;
                    result.ActionDescription = "Archived";
                    break;

                case FilterAction.MarkAsSpam:
                    await _emailService.MarkAsSpamAsync(message.Id, cancellationToken);
                    result.ActionTaken = true;
                    result.ActionDescription = "Marked as spam";
                    break;
            }

            // Send auto-reply if configured
            if (!string.IsNullOrWhiteSpace(rule.AutoReplyTemplateId))
            {
                await SendAutoReplyAsync(message, rule.AutoReplyTemplateId, cancellationToken);
                result.ActionDescription += " + Auto-reply sent";
            }

            _logger.LogInformation(
                "Action taken on message {MessageId}: {Action}",
                message.Id, result.ActionDescription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking action on message {MessageId}", message.Id);
            result.Error = $"Action error: {ex.Message}";
        }
    }

    private async Task SendAutoReplyAsync(
        EmailMessage message,
        string templateId,
        CancellationToken cancellationToken)
    {
        var template = _config.AutoReplyTemplates.FirstOrDefault(t => t.Id == templateId);
        if (template == null)
        {
            _logger.LogWarning("Auto-reply template {TemplateId} not found", templateId);
            return;
        }

        var subject = template.Subject
            .Replace("{originalSubject}", message.Subject);

        var body = template.Body
            .Replace("{sender}", message.FromName ?? message.From)
            .Replace("{originalSubject}", message.Subject);

        if (template.IncludeOriginal)
        {
            body += "\n\n--- Original Message ---\n" + message.Body;
        }

        await _emailService.SendReplyAsync(message.Id, subject, body, cancellationToken);
        _logger.LogInformation("Sent auto-reply to {From} using template {TemplateId}", message.From, templateId);
    }
}

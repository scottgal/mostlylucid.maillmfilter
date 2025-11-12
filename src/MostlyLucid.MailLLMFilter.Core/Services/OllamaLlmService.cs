using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using OllamaSharp;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Ollama-based LLM service implementation
/// </summary>
public class OllamaLlmService : ILlmService
{
    private readonly FilterConfiguration _config;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaLlmService> _logger;
    private readonly OllamaApiClient _client;

    public OllamaLlmService(IOptions<FilterConfiguration> config, ILogger<OllamaLlmService> logger)
    {
        _config = config.Value;
        _settings = config.Value.Ollama;
        _logger = logger;
        _client = new OllamaApiClient(_settings.Endpoint, _settings.Model);
    }

    public async Task<LlmAnalysisResult> AnalyzeEmailAsync(EmailMessage message, FilterRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get LLM filter template if specified
            LlmFilterTemplate? template = null;
            if (!string.IsNullOrWhiteSpace(rule.LlmFilterTemplateId))
            {
                template = _config.LlmFilterTemplates.FirstOrDefault(t => t.Id == rule.LlmFilterTemplateId);
                if (template != null)
                {
                    _logger.LogDebug("Using LLM template {TemplateId} for rule {RuleName}", template.Id, rule.Name);
                }
            }

            var prompt = template != null
                ? BuildPromptFromTemplate(message, rule, template)
                : BuildPrompt(message, rule);

            _logger.LogDebug("Analyzing email {MessageId} with rule {RuleName}", message.Id, rule.Name);

            // Use template settings if available, otherwise use global settings
            var model = template?.Model ?? _settings.Model;
            var temperature = template?.Temperature ?? _settings.Temperature;
            var maxTokens = template?.MaxTokens ?? _settings.MaxTokens;

            var response = await _client.Generate(new GenerateRequest
            {
                Prompt = prompt,
                Model = model,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            }, cancellationToken);

            var fullResponse = response?.Response ?? string.Empty;

            _logger.LogDebug("LLM Response: {Response}", fullResponse);

            return ParseLlmResponse(fullResponse, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing email with LLM");
            return new LlmAnalysisResult
            {
                IsMatch = false,
                Confidence = 0,
                Reason = $"Error: {ex.Message}",
                FullResponse = string.Empty
            };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _client.ListLocalModels(cancellationToken);
            return models?.Any(m => m.Name == _settings.Model) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama service not available");
            return false;
        }
    }

    public async Task<LlmAnalysisResult> TestTemplateAsync(EmailMessage message, LlmFilterTemplate template, FilterRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildPromptFromTemplate(message, rule, template);
            _logger.LogInformation("Testing LLM template {TemplateId} with sample email", template.Id);

            // Use template settings if available, otherwise use global settings
            var model = template.Model ?? _settings.Model;
            var temperature = template.Temperature ?? _settings.Temperature;
            var maxTokens = template.MaxTokens ?? _settings.MaxTokens;

            var response = await _client.Generate(new GenerateRequest
            {
                Prompt = prompt,
                Model = model,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            }, cancellationToken);

            var fullResponse = response?.Response ?? string.Empty;

            _logger.LogDebug("LLM Test Response: {Response}", fullResponse);

            return ParseLlmResponse(fullResponse, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing LLM template");
            return new LlmAnalysisResult
            {
                IsMatch = false,
                Confidence = 0,
                Reason = $"Error: {ex.Message}",
                FullResponse = string.Empty
            };
        }
    }

    private string BuildPrompt(EmailMessage message, FilterRule rule)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an email filter assistant. Analyze the following email and determine if it matches the filter criteria.");
        sb.AppendLine();
        sb.AppendLine("EMAIL DETAILS:");
        sb.AppendLine($"From: {message.FromName ?? message.From}");
        sb.AppendLine($"Subject: {message.Subject}");
        sb.AppendLine($"Body: {TruncateBody(message.Body, 1000)}");
        sb.AppendLine();
        sb.AppendLine("FILTER CRITERIA:");

        if (rule.Keywords.Any())
        {
            sb.AppendLine($"Keywords to match: {string.Join(", ", rule.Keywords)}");
        }

        if (rule.Topics.Any())
        {
            sb.AppendLine($"Topics to match: {string.Join(", ", rule.Topics)}");
        }

        if (rule.Mentions.Any())
        {
            sb.AppendLine($"Mentions to look for: {string.Join(", ", rule.Mentions)}");
        }

        if (!string.IsNullOrWhiteSpace(rule.CustomPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("ADDITIONAL INSTRUCTIONS:");
            sb.AppendLine(rule.CustomPrompt);
        }

        sb.AppendLine();
        sb.AppendLine("Respond in the following JSON format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"match\": true/false,");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"reason\": \"explanation of why it matched or didn't\",");
        sb.AppendLine("  \"topics\": [\"detected\", \"topics\"],");
        sb.AppendLine("  \"mentions\": [\"detected\", \"mentions\"]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string BuildPromptFromTemplate(EmailMessage message, FilterRule rule, LlmFilterTemplate template)
    {
        var sb = new StringBuilder();

        // Add system prompt if provided
        if (!string.IsNullOrWhiteSpace(template.SystemPrompt))
        {
            sb.AppendLine(template.SystemPrompt);
            sb.AppendLine();
        }

        // Add few-shot examples if provided
        if (template.Examples.Any())
        {
            sb.AppendLine("EXAMPLES:");
            foreach (var example in template.Examples)
            {
                sb.AppendLine($"Example Subject: {example.Subject}");
                sb.AppendLine($"Example Body: {example.Body}");
                sb.AppendLine($"Expected Result: {example.ExpectedResult}");
                sb.AppendLine($"Explanation: {example.Explanation}");
                sb.AppendLine();
            }
        }

        // Build prompt from template with placeholder replacement
        var promptText = template.PromptTemplate
            .Replace("{from}", message.FromName ?? message.From)
            .Replace("{subject}", message.Subject)
            .Replace("{body}", TruncateBody(message.Body, 1000))
            .Replace("{keywords}", template.RequiresKeywords && rule.Keywords.Any()
                ? string.Join(", ", rule.Keywords)
                : "None")
            .Replace("{topics}", template.RequiresTopics && rule.Topics.Any()
                ? string.Join(", ", rule.Topics)
                : "None")
            .Replace("{mentions}", template.RequiresMentions && rule.Mentions.Any()
                ? string.Join(", ", rule.Mentions)
                : "None");

        sb.AppendLine(promptText);
        sb.AppendLine();

        // Add output format
        if (!string.IsNullOrWhiteSpace(template.OutputFormat))
        {
            sb.AppendLine("RESPONSE FORMAT:");
            sb.AppendLine(template.OutputFormat);
        }

        return sb.ToString();
    }

    private string TruncateBody(string body, int maxLength)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= maxLength)
            return body;

        return body.Substring(0, maxLength) + "... [truncated]";
    }

    private LlmAnalysisResult ParseLlmResponse(string response, EmailMessage message)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (!jsonMatch.Success)
            {
                // Fallback: try to parse the entire response
                return ParseFallback(response);
            }

            var jsonString = jsonMatch.Value;
            var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var result = new LlmAnalysisResult
            {
                IsMatch = root.TryGetProperty("match", out var matchProp) && matchProp.GetBoolean(),
                Confidence = root.TryGetProperty("confidence", out var confProp) ? (float)confProp.GetDouble() : 0f,
                Reason = root.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() ?? string.Empty : string.Empty,
                FullResponse = response
            };

            if (root.TryGetProperty("topics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array)
            {
                result.DetectedTopics = topicsProp.EnumerateArray()
                    .Select(t => t.GetString() ?? string.Empty)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }

            if (root.TryGetProperty("mentions", out var mentionsProp) && mentionsProp.ValueKind == JsonValueKind.Array)
            {
                result.DetectedMentions = mentionsProp.EnumerateArray()
                    .Select(m => m.GetString() ?? string.Empty)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON, using fallback");
            return ParseFallback(response);
        }
    }

    private LlmAnalysisResult ParseFallback(string response)
    {
        // Simple fallback parsing based on keywords
        var lowerResponse = response.ToLowerInvariant();
        var isMatch = lowerResponse.Contains("match") || lowerResponse.Contains("yes") || lowerResponse.Contains("true");

        float confidence = 0.5f;
        if (lowerResponse.Contains("high confidence") || lowerResponse.Contains("definitely"))
            confidence = 0.9f;
        else if (lowerResponse.Contains("low confidence") || lowerResponse.Contains("maybe"))
            confidence = 0.3f;

        return new LlmAnalysisResult
        {
            IsMatch = isMatch,
            Confidence = confidence,
            Reason = response,
            FullResponse = response
        };
    }
}

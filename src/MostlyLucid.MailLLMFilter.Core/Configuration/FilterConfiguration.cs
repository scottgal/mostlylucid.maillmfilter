namespace MostlyLucid.MailLLMFilter.Core.Configuration;

/// <summary>
/// Root configuration for the mail filter application
/// </summary>
public class FilterConfiguration
{
    /// <summary>
    /// Ollama service settings
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Gmail service settings
    /// </summary>
    public GmailSettings Gmail { get; set; } = new();

    /// <summary>
    /// Filter rules for processing emails
    /// </summary>
    public List<FilterRule> FilterRules { get; set; } = new();

    /// <summary>
    /// Auto-reply templates for filtered messages
    /// </summary>
    public List<AutoReplyTemplate> AutoReplyTemplates { get; set; } = new();
}

/// <summary>
/// Ollama LLM service settings
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Ollama API endpoint (default: http://localhost:11434)
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use (default: llama3.2)
    /// </summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>
    /// Temperature for LLM responses (0.0 - 1.0)
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Maximum tokens for LLM response
    /// </summary>
    public int MaxTokens { get; set; } = 500;
}

/// <summary>
/// Gmail API settings
/// </summary>
public class GmailSettings
{
    /// <summary>
    /// Path to Gmail API credentials JSON file
    /// </summary>
    public string CredentialsPath { get; set; } = "credentials.json";

    /// <summary>
    /// Gmail label to move filtered messages to (null = delete)
    /// </summary>
    public string? FilteredLabel { get; set; } = "Filtered";

    /// <summary>
    /// How often to check for new messages (in seconds)
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum messages to process per check
    /// </summary>
    public int MaxMessagesPerCheck { get; set; } = 50;
}

/// <summary>
/// A filter rule for matching and processing emails
/// </summary>
public class FilterRule
{
    /// <summary>
    /// Rule name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Keywords to match (exact or partial match)
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Topics to match using LLM analysis
    /// </summary>
    public List<string> Topics { get; set; } = new();

    /// <summary>
    /// Specific mentions to look for (e.g., "Prof. Galloway")
    /// </summary>
    public List<string> Mentions { get; set; } = new();

    /// <summary>
    /// Minimum confidence level to trigger action (0.0 - 1.0)
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Action to take when rule matches
    /// </summary>
    public FilterAction Action { get; set; } = FilterAction.MoveToFolder;

    /// <summary>
    /// Custom folder/label to move to (if Action is MoveToFolder)
    /// </summary>
    public string? TargetFolder { get; set; }

    /// <summary>
    /// Auto-reply template ID to use (if configured)
    /// </summary>
    public string? AutoReplyTemplateId { get; set; }

    /// <summary>
    /// LLM prompt template for analyzing this rule
    /// </summary>
    public string? CustomPrompt { get; set; }
}

/// <summary>
/// Actions that can be taken on filtered messages
/// </summary>
public enum FilterAction
{
    /// <summary>
    /// Move to specified folder/label
    /// </summary>
    MoveToFolder,

    /// <summary>
    /// Delete the message
    /// </summary>
    Delete,

    /// <summary>
    /// Mark as read
    /// </summary>
    MarkAsRead,

    /// <summary>
    /// Archive the message
    /// </summary>
    Archive
}

/// <summary>
/// Auto-reply template for filtered messages
/// </summary>
public class AutoReplyTemplate
{
    /// <summary>
    /// Template identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Template name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email subject line
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Email body template (can use placeholders like {sender}, {originalSubject})
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include original message in reply
    /// </summary>
    public bool IncludeOriginal { get; set; } = false;
}

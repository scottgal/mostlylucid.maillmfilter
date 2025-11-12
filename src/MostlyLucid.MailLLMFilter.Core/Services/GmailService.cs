using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// Gmail API service implementation
/// </summary>
public class GmailService : IGmailService
{
    private readonly GmailSettings _settings;
    private readonly ILogger<GmailService> _logger;
    private GmailService? _service;
    private UserCredential? _credential;

    private static readonly string[] Scopes = {
        GmailService.Scope.GmailModify,
        GmailService.Scope.GmailSend
    };

    public bool IsAuthenticated => _credential != null && _service != null;

    public GmailService(IOptions<FilterConfiguration> config, ILogger<GmailService> logger)
    {
        _settings = config.Value.Gmail;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Gmail service");

            if (!File.Exists(_settings.CredentialsPath))
            {
                throw new FileNotFoundException($"Gmail credentials file not found at: {_settings.CredentialsPath}");
            }

            using var stream = new FileStream(_settings.CredentialsPath, FileMode.Open, FileAccess.Read);
            var credPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MostlyLucid.MailLLMFilter"
            );

            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(credPath, true)
            );

            _service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "MostlyLucid Mail LLM Filter"
            });

            _logger.LogInformation("Gmail service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Gmail service");
            throw;
        }
    }

    public async Task<List<EmailMessage>> GetUnreadMessagesAsync(int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized. Call InitializeAsync first.");

        try
        {
            var request = _service.Users.Messages.List("me");
            request.Q = "is:unread";
            request.MaxResults = maxResults;

            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Messages == null || !response.Messages.Any())
            {
                return new List<EmailMessage>();
            }

            var messages = new List<EmailMessage>();

            foreach (var messageInfo in response.Messages)
            {
                var message = await GetMessageDetailsAsync(messageInfo.Id, cancellationToken);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            _logger.LogInformation("Retrieved {Count} unread messages", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread messages");
            throw;
        }
    }

    public async Task MoveToLabelAsync(string messageId, string labelName, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized");

        try
        {
            var labelId = await GetOrCreateLabelAsync(labelName, cancellationToken);

            var modifyRequest = new ModifyMessageRequest
            {
                AddLabelIds = new List<string> { labelId },
                RemoveLabelIds = new List<string> { "INBOX" }
            };

            await _service.Users.Messages.Modify(modifyRequest, "me", messageId).ExecuteAsync(cancellationToken);
            _logger.LogInformation("Moved message {MessageId} to label {LabelName}", messageId, labelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving message to label");
            throw;
        }
    }

    public async Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized");

        try
        {
            await _service.Users.Messages.Trash("me", messageId).ExecuteAsync(cancellationToken);
            _logger.LogInformation("Deleted message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message");
            throw;
        }
    }

    public async Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized");

        try
        {
            var modifyRequest = new ModifyMessageRequest
            {
                RemoveLabelIds = new List<string> { "UNREAD" }
            };

            await _service.Users.Messages.Modify(modifyRequest, "me", messageId).ExecuteAsync(cancellationToken);
            _logger.LogInformation("Marked message {MessageId} as read", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read");
            throw;
        }
    }

    public async Task ArchiveMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized");

        try
        {
            var modifyRequest = new ModifyMessageRequest
            {
                RemoveLabelIds = new List<string> { "INBOX" }
            };

            await _service.Users.Messages.Modify(modifyRequest, "me", messageId).ExecuteAsync(cancellationToken);
            _logger.LogInformation("Archived message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving message");
            throw;
        }
    }

    public async Task SendReplyAsync(string messageId, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (_service == null)
            throw new InvalidOperationException("Gmail service not initialized");

        try
        {
            var originalMessage = await _service.Users.Messages.Get("me", messageId).ExecuteAsync(cancellationToken);
            var fromHeader = originalMessage.Payload.Headers.FirstOrDefault(h => h.Name.Equals("From", StringComparison.OrdinalIgnoreCase));

            if (fromHeader == null)
            {
                throw new InvalidOperationException("Could not find sender address");
            }

            var to = ExtractEmailAddress(fromHeader.Value);

            var emailMessage = new StringBuilder();
            emailMessage.AppendLine($"To: {to}");
            emailMessage.AppendLine($"Subject: {subject}");
            emailMessage.AppendLine($"In-Reply-To: {messageId}");
            emailMessage.AppendLine("Content-Type: text/plain; charset=utf-8");
            emailMessage.AppendLine();
            emailMessage.AppendLine(body);

            var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(emailMessage.ToString()))
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");

            var message = new Message { Raw = encodedMessage };

            await _service.Users.Messages.Send(message, "me").ExecuteAsync(cancellationToken);
            _logger.LogInformation("Sent reply to message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reply");
            throw;
        }
    }

    private async Task<EmailMessage?> GetMessageDetailsAsync(string messageId, CancellationToken cancellationToken)
    {
        try
        {
            var message = await _service!.Users.Messages.Get("me", messageId).ExecuteAsync(cancellationToken);

            var headers = message.Payload.Headers;
            var from = headers.FirstOrDefault(h => h.Name.Equals("From", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            var subject = headers.FirstOrDefault(h => h.Name.Equals("Subject", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            var to = headers.FirstOrDefault(h => h.Name.Equals("To", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            var dateHeader = headers.FirstOrDefault(h => h.Name.Equals("Date", StringComparison.OrdinalIgnoreCase))?.Value;

            var body = GetMessageBody(message.Payload);

            var emailMessage = new EmailMessage
            {
                Id = message.Id,
                ThreadId = message.ThreadId,
                From = ExtractEmailAddress(from),
                FromName = ExtractName(from),
                To = to.Split(',').Select(t => ExtractEmailAddress(t.Trim())).ToList(),
                Subject = subject,
                Body = body,
                ReceivedDate = ParseDate(dateHeader),
                Labels = message.LabelIds?.ToList() ?? new List<string>(),
                IsUnread = message.LabelIds?.Contains("UNREAD") ?? false,
                Snippet = message.Snippet
            };

            return emailMessage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting message details for {MessageId}", messageId);
            return null;
        }
    }

    private string GetMessageBody(MessagePart payload)
    {
        if (payload.Body?.Data != null)
        {
            return DecodeBase64(payload.Body.Data);
        }

        if (payload.Parts != null)
        {
            foreach (var part in payload.Parts)
            {
                if (part.MimeType == "text/plain" && part.Body?.Data != null)
                {
                    return DecodeBase64(part.Body.Data);
                }
            }

            foreach (var part in payload.Parts)
            {
                if (part.MimeType == "text/html" && part.Body?.Data != null)
                {
                    return DecodeBase64(part.Body.Data);
                }
            }

            foreach (var part in payload.Parts)
            {
                var body = GetMessageBody(part);
                if (!string.IsNullOrEmpty(body))
                    return body;
            }
        }

        return string.Empty;
    }

    private string DecodeBase64(string base64)
    {
        var data = base64.Replace('-', '+').Replace('_', '/');
        switch (data.Length % 4)
        {
            case 2: data += "=="; break;
            case 3: data += "="; break;
        }
        var bytes = Convert.FromBase64String(data);
        return Encoding.UTF8.GetString(bytes);
    }

    private string ExtractEmailAddress(string fromHeader)
    {
        var match = Regex.Match(fromHeader, @"<(.+?)>");
        return match.Success ? match.Groups[1].Value : fromHeader.Trim();
    }

    private string? ExtractName(string fromHeader)
    {
        var match = Regex.Match(fromHeader, @"^(.+?)\s*<");
        return match.Success ? match.Groups[1].Value.Trim().Trim('"') : null;
    }

    private DateTime ParseDate(string? dateHeader)
    {
        if (string.IsNullOrWhiteSpace(dateHeader))
            return DateTime.UtcNow;

        if (DateTime.TryParse(dateHeader, out var date))
            return date.ToUniversalTime();

        return DateTime.UtcNow;
    }

    private async Task<string> GetOrCreateLabelAsync(string labelName, CancellationToken cancellationToken)
    {
        var labelsResponse = await _service!.Users.Labels.List("me").ExecuteAsync(cancellationToken);
        var existingLabel = labelsResponse.Labels?.FirstOrDefault(l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

        if (existingLabel != null)
        {
            return existingLabel.Id;
        }

        var newLabel = new Label
        {
            Name = labelName,
            LabelListVisibility = "labelShow",
            MessageListVisibility = "show"
        };

        var createdLabel = await _service.Users.Labels.Create(newLabel, "me").ExecuteAsync(cancellationToken);
        _logger.LogInformation("Created new label: {LabelName}", labelName);

        return createdLabel.Id;
    }
}

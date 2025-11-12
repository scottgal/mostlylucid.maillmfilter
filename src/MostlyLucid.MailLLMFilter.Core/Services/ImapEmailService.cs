using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MostlyLucid.MailLLMFilter.Core.Configuration;
using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.Core.Services;

/// <summary>
/// IMAP email service implementation using MailKit
/// </summary>
public class ImapEmailService : IEmailService, IDisposable
{
    private readonly ImapSettings _settings;
    private readonly ILogger<ImapEmailService> _logger;
    private ImapClient? _imapClient;
    private bool _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated;

    public string ProviderName => "IMAP";

    public ImapEmailService(IOptions<FilterConfiguration> config, ILogger<ImapEmailService> logger)
    {
        _settings = config.Value.Imap;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing IMAP service for {Server}", _settings.Server);

            if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException("IMAP username and password must be configured");
            }

            _imapClient = new ImapClient();
            await _imapClient.ConnectAsync(_settings.Server, _settings.Port, _settings.UseSsl, cancellationToken);
            await _imapClient.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

            _isAuthenticated = true;
            _logger.LogInformation("IMAP service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize IMAP service");
            _isAuthenticated = false;
            throw;
        }
    }

    public async Task<List<EmailMessage>> GetUnreadMessagesAsync(int maxResults = 50, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        try
        {
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);

            var messages = new List<EmailMessage>();
            var count = Math.Min(uids.Count, maxResults);

            for (int i = 0; i < count; i++)
            {
                var message = await GetMessageDetailsAsync(inbox, uids[i], cancellationToken);
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

    public async Task MoveToFolderAsync(string messageId, string folderName, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Get or create target folder
            var targetFolder = await GetOrCreateFolderAsync(folderName, cancellationToken);

            // Move message
            await inbox.MoveToAsync(uid, targetFolder, cancellationToken);
            _logger.LogInformation("Moved message {MessageId} to folder {FolderName}", messageId, folderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving message to folder");
            throw;
        }
    }

    public async Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Move to trash
            var trashFolder = await GetOrCreateFolderAsync(_settings.TrashFolder, cancellationToken);
            await inbox.MoveToAsync(uid, trashFolder, cancellationToken);

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
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
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
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Move to archive folder
            var archiveFolder = await GetOrCreateFolderAsync(_settings.ArchiveFolder, cancellationToken);
            await inbox.MoveToAsync(uid, archiveFolder, cancellationToken);

            _logger.LogInformation("Archived message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving message");
            throw;
        }
    }

    public async Task MarkAsSpamAsync(string messageId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Move to spam folder
            var spamFolder = await GetOrCreateFolderAsync(_settings.SpamFolder, cancellationToken);
            await inbox.MoveToAsync(uid, spamFolder, cancellationToken);

            _logger.LogInformation("Marked message {MessageId} as spam", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as spam");
            throw;
        }
    }

    public async Task SendReplyAsync(string messageId, string subject, string body, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        try
        {
            var uid = ParseUniqueId(messageId);
            var inbox = await _imapClient!.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            var originalMessage = await inbox.GetMessageAsync(uid, cancellationToken);

            var replyMessage = new MimeMessage();
            replyMessage.From.Add(new MailboxAddress(_settings.Username, _settings.Username));
            replyMessage.To.Add(originalMessage.From[0]);
            replyMessage.Subject = subject;
            replyMessage.InReplyTo = originalMessage.MessageId;

            var builder = new BodyBuilder { TextBody = body };
            replyMessage.Body = builder.ToMessageBody();

            using var smtpClient = new SmtpClient();
            await smtpClient.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, _settings.SmtpUseSsl, cancellationToken);
            await smtpClient.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await smtpClient.SendAsync(replyMessage, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Sent reply to message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reply");
            throw;
        }
    }

    private async Task<EmailMessage?> GetMessageDetailsAsync(IMailFolder folder, UniqueId uid, CancellationToken cancellationToken)
    {
        try
        {
            var message = await folder.GetMessageAsync(uid, cancellationToken);

            var emailMessage = new EmailMessage
            {
                Id = $"{folder.Name}:{uid.Id}",
                ThreadId = message.MessageId ?? string.Empty,
                From = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
                FromName = message.From.Mailboxes.FirstOrDefault()?.Name,
                To = message.To.Mailboxes.Select(m => m.Address).ToList(),
                Subject = message.Subject ?? string.Empty,
                Body = message.TextBody ?? message.HtmlBody ?? string.Empty,
                ReceivedDate = message.Date.UtcDateTime,
                Labels = new List<string>(),
                IsUnread = !message.Flags.HasFlag(MessageFlags.Seen),
                Snippet = GetSnippet(message.TextBody ?? message.HtmlBody ?? string.Empty)
            };

            return emailMessage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting message details for UID {Uid}", uid);
            return null;
        }
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(string folderName, CancellationToken cancellationToken)
    {
        try
        {
            var folder = await _imapClient!.GetFolderAsync(folderName, cancellationToken);
            return folder;
        }
        catch
        {
            // Folder doesn't exist, create it
            var personal = _imapClient!.GetFolder(_imapClient.PersonalNamespaces[0]);
            var folder = await personal.CreateAsync(folderName, true, cancellationToken);
            _logger.LogInformation("Created folder: {FolderName}", folderName);
            return folder;
        }
    }

    private UniqueId ParseUniqueId(string messageId)
    {
        // Format: "FolderName:UniqueId"
        var parts = messageId.Split(':');
        if (parts.Length != 2 || !uint.TryParse(parts[1], out var id))
        {
            throw new ArgumentException($"Invalid message ID format: {messageId}");
        }

        return new UniqueId(id);
    }

    private string GetSnippet(string text, int maxLength = 150)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var snippet = text.Replace("\n", " ").Replace("\r", " ").Trim();
        return snippet.Length > maxLength ? snippet.Substring(0, maxLength) + "..." : snippet;
    }

    private void EnsureAuthenticated()
    {
        if (!_isAuthenticated || _imapClient == null)
        {
            throw new InvalidOperationException("IMAP service not authenticated. Call InitializeAsync first.");
        }
    }

    public void Dispose()
    {
        if (_imapClient?.IsConnected == true)
        {
            _imapClient.Disconnect(true);
        }
        _imapClient?.Dispose();
    }
}

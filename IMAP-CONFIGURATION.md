# IMAP Configuration Guide

This guide explains how to configure the Mail LLM Filter to use IMAP instead of Gmail API.

## Features Added

### 1. **Mark as Spam Action**

The application now supports marking emails as spam. You can use this in your filter rules:

```json
{
  "Name": "Spam Filter",
  "Enabled": true,
  "Action": "MarkAsSpam",
  "ConfidenceThreshold": 0.8
}
```

For Gmail: Messages are moved to the SPAM label
For IMAP: Messages are moved to the configured spam folder

### 2. **Generic IMAP Support**

The application now has a pluggable email provider architecture:
- **Gmail** (default): Uses Gmail API with OAuth 2.0
- **IMAP**: Uses standard IMAP protocol (works with Gmail, Outlook, Yahoo, etc.)

## Switching to IMAP

### Step 1: Configure Email Provider

In your `appsettings.json`, set the email provider:

```json
{
  "FilterConfiguration": {
    "EmailProvider": "IMAP",
    ...
  }
}
```

### Step 2: Configure IMAP Settings

Add IMAP configuration to `appsettings.json`:

```json
{
  "FilterConfiguration": {
    "EmailProvider": "IMAP",
    "Imap": {
      "Server": "imap.gmail.com",
      "Port": 993,
      "UseSsl": true,
      "Username": "your.email@gmail.com",
      "Password": "your-app-password",
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "SmtpUseSsl": false,
      "InboxFolder": "INBOX",
      "SpamFolder": "[Gmail]/Spam",
      "TrashFolder": "[Gmail]/Trash",
      "ArchiveFolder": "[Gmail]/All Mail",
      "FilteredFolder": "Filtered",
      "CheckIntervalSeconds": 60,
      "MaxMessagesPerCheck": 50
    }
  }
}
```

## IMAP Configuration Options

### Required Settings

| Setting | Description | Example |
|---------|-------------|---------|
| `Server` | IMAP server hostname | `imap.gmail.com` |
| `Port` | IMAP port (usually 993 for SSL) | `993` |
| `UseSsl` | Use SSL/TLS connection | `true` |
| `Username` | Email account username | `user@gmail.com` |
| `Password` | Email account password or app password | `your-app-password` |

### SMTP Settings (for sending replies)

| Setting | Description | Example |
|---------|-------------|---------|
| `SmtpServer` | SMTP server hostname | `smtp.gmail.com` |
| `SmtpPort` | SMTP port (587 for TLS, 465 for SSL) | `587` |
| `SmtpUseSsl` | Use SSL (true for 465, false for 587) | `false` |

### Folder Settings

| Setting | Description | Gmail Default | Outlook Default |
|---------|-------------|---------------|-----------------|
| `InboxFolder` | Inbox folder name | `INBOX` | `INBOX` |
| `SpamFolder` | Spam/Junk folder | `[Gmail]/Spam` | `Junk` |
| `TrashFolder` | Trash/Deleted folder | `[Gmail]/Trash` | `Deleted Items` |
| `ArchiveFolder` | Archive folder | `[Gmail]/All Mail` | `Archive` |
| `FilteredFolder` | Custom filtered messages folder | `Filtered` | `Filtered` |

## Provider-Specific Examples

### Gmail via IMAP

**Important**: You must create an App Password for Gmail:
1. Go to https://myaccount.google.com/security
2. Enable 2-Step Verification
3. Generate an App Password under "App passwords"
4. Use the generated password in configuration

```json
{
  "Imap": {
    "Server": "imap.gmail.com",
    "Port": 993,
    "UseSsl": true,
    "Username": "your.email@gmail.com",
    "Password": "16-character-app-password",
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUseSsl": false,
    "SpamFolder": "[Gmail]/Spam",
    "TrashFolder": "[Gmail]/Trash",
    "ArchiveFolder": "[Gmail]/All Mail"
  }
}
```

### Microsoft Outlook/Office 365

```json
{
  "Imap": {
    "Server": "outlook.office365.com",
    "Port": 993,
    "UseSsl": true,
    "Username": "your.email@outlook.com",
    "Password": "your-password",
    "SmtpServer": "smtp.office365.com",
    "SmtpPort": 587,
    "SmtpUseSsl": false,
    "SpamFolder": "Junk",
    "TrashFolder": "Deleted Items",
    "ArchiveFolder": "Archive"
  }
}
```

### Yahoo Mail

```json
{
  "Imap": {
    "Server": "imap.mail.yahoo.com",
    "Port": 993,
    "UseSsl": true,
    "Username": "your.email@yahoo.com",
    "Password": "your-app-password",
    "SmtpServer": "smtp.mail.yahoo.com",
    "SmtpPort": 587,
    "SmtpUseSsl": false,
    "SpamFolder": "Bulk Mail",
    "TrashFolder": "Trash",
    "ArchiveFolder": "Archive"
  }
}
```

### Custom IMAP Server

```json
{
  "Imap": {
    "Server": "mail.example.com",
    "Port": 993,
    "UseSsl": true,
    "Username": "user@example.com",
    "Password": "your-password",
    "SmtpServer": "mail.example.com",
    "SmtpPort": 587,
    "SmtpUseSsl": false,
    "SpamFolder": "Spam",
    "TrashFolder": "Trash",
    "ArchiveFolder": "Archive"
  }
}
```

## Security Considerations

### Storing Passwords

**WARNING**: The IMAP configuration stores passwords in plain text in `appsettings.json`.

**Best practices**:
1. **Use App Passwords** when available (Gmail, Yahoo)
2. **Protect the file**: Set restrictive file permissions on `appsettings.json`
3. **Use environment variables**: You can override settings with environment variables:
   ```bash
   export FilterConfiguration__Imap__Password="your-password"
   ```
4. **Consider encryption**: For production use, consider using .NET Secret Manager or Azure Key Vault

### Gmail Security

- Gmail requires OAuth 2.0 or App Passwords
- IMAP access must be enabled in Gmail settings
- Less secure app access is deprecated

### Enable IMAP in Gmail

1. Log into Gmail
2. Click the gear icon → "See all settings"
3. Go to "Forwarding and POP/IMAP" tab
4. Under "IMAP access", select "Enable IMAP"
5. Click "Save Changes"

## Testing IMAP Configuration

### Test Connection

1. Update `appsettings.json` with your IMAP settings
2. Run the application
3. Click "Connect" button
4. You should see "Connected to IMAP" in the status bar

### Troubleshooting

**Connection refused or timeout**:
- Check server and port are correct
- Verify firewall/antivirus isn't blocking connection
- Ensure IMAP is enabled in your email provider settings

**Authentication failed**:
- Verify username and password are correct
- For Gmail: Ensure you're using an App Password, not your regular password
- Check if 2FA is required

**Folders not found**:
- Use IMAP client tools (like Thunderbird) to verify folder names
- Folder names are case-sensitive
- Gmail uses `[Gmail]/` prefix for system folders

**SSL/TLS errors**:
- Try changing `UseSsl` setting
- For port 587 (STARTTLS), use `UseSsl: false`
- For port 465 or 993 (SSL), use `UseSsl: true`

## Comparison: Gmail API vs IMAP

| Feature | Gmail API | IMAP |
|---------|-----------|------|
| **Setup Complexity** | High (OAuth 2.0) | Low (username/password) |
| **Security** | OAuth 2.0 tokens | Password or App Password |
| **Speed** | Faster (batched operations) | Slower (individual operations) |
| **Provider Support** | Gmail only | Any IMAP provider |
| **Folder Creation** | Automatic | Automatic |
| **Spam Detection** | Uses Gmail's SPAM label | Moves to spam folder |
| **Rate Limits** | 1 billion quota units/day | Varies by provider |

## Filter Rule Examples with New Actions

### Spam Filtering

```json
{
  "Name": "Aggressive Spam Filter",
  "Enabled": true,
  "Keywords": ["viagra", "casino", "prize", "winner"],
  "Topics": ["spam", "phishing", "scam"],
  "ConfidenceThreshold": 0.85,
  "Action": "MarkAsSpam"
}
```

### Combined with Auto-Reply

```json
{
  "Name": "Vacation Auto-Reply with Spam Check",
  "Enabled": true,
  "Keywords": ["meeting", "urgent", "important"],
  "ConfidenceThreshold": 0.7,
  "Action": "MarkAsRead",
  "AutoReplyTemplateId": "vacation-reply"
}
```

## Migration from Gmail API to IMAP

If you're switching from Gmail API to IMAP:

1. **Backup**: Keep your `credentials.json` file as backup
2. **Update config**: Change `EmailProvider` to `"IMAP"`
3. **Add IMAP settings**: Add the `Imap` section to your config
4. **Test**: Run the application and test connection
5. **Update folder names**: Adjust folder names in filter rules if needed

**Note**: Gmail API uses "labels" while IMAP uses "folders", but the application abstracts this difference.

## Complete Configuration Example

```json
{
  "FilterConfiguration": {
    "EmailProvider": "IMAP",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2",
      "Temperature": 0.3,
      "MaxTokens": 500
    },
    "Imap": {
      "Server": "imap.gmail.com",
      "Port": 993,
      "UseSsl": true,
      "Username": "your.email@gmail.com",
      "Password": "your-app-password",
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "SmtpUseSsl": false,
      "InboxFolder": "INBOX",
      "SpamFolder": "[Gmail]/Spam",
      "TrashFolder": "[Gmail]/Trash",
      "ArchiveFolder": "[Gmail]/All Mail",
      "FilteredFolder": "Filtered",
      "CheckIntervalSeconds": 60,
      "MaxMessagesPerCheck": 50
    },
    "FilterRules": [
      {
        "Name": "Spam Filter",
        "Enabled": true,
        "Keywords": ["spam", "phishing"],
        "Topics": ["spam", "scam"],
        "ConfidenceThreshold": 0.85,
        "Action": "MarkAsSpam"
      },
      {
        "Name": "Newsletter Organizer",
        "Enabled": true,
        "Keywords": ["newsletter", "digest"],
        "ConfidenceThreshold": 0.6,
        "Action": "MoveToFolder",
        "TargetFolder": "Newsletters"
      }
    ]
  }
}
```

## NuGet Dependency

The IMAP implementation uses **MailKit** library (v4.8.0):
- Cross-platform email library
- Supports IMAP, POP3, and SMTP
- Actively maintained by Jeffrey Stedfast
- MIT Licensed

This is automatically installed when you build the project.

## Support

For issues related to IMAP configuration:
- Check your email provider's documentation for correct IMAP settings
- Review logs in Debug mode for detailed error messages
- Ensure firewall/antivirus allows IMAP connections (port 993/143)

For Gmail-specific issues:
- Review Gmail's IMAP documentation: https://support.google.com/mail/answer/7126229
- Ensure "Less secure app access" is not required (deprecated)
- Use App Passwords: https://support.google.com/accounts/answer/185833

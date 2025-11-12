# MostlyLucid Mail LLM Filter

An intelligent email filtering application for Gmail that uses LLM (Large Language Model) powered analysis to automatically categorize, filter, and respond to incoming emails.

## Features

- **LLM-Powered Filtering**: Uses Ollama with Llama 3.2 to intelligently analyze email content
- **Configurable Rules**: Define custom filter rules with keywords, topics, and mentions
- **Multiple Actions**: Move to folder, delete, mark as read, or archive filtered messages
- **Auto-Reply**: Automatically send customizable reply emails to filtered messages
- **Windows Notifications**: Get toast notifications when emails are processed
- **Auto-Check**: Automatically check for new emails at configurable intervals
- **Windows Integration**: Native WPF application with modern UI

## Requirements

- Windows 10/11
- .NET 9.0 SDK
- [Ollama](https://ollama.ai/) installed and running locally
- Gmail account with API access enabled

## Setup

### 1. Install Ollama

Download and install Ollama from [https://ollama.ai/](https://ollama.ai/)

Pull the Llama 3.2 model:
```bash
ollama pull llama3.2
```

### 2. Set Up Gmail API

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or select an existing one)
3. Enable the Gmail API:
   - Navigate to "APIs & Services" > "Library"
   - Search for "Gmail API"
   - Click "Enable"
4. Create credentials:
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "OAuth client ID"
   - Select "Desktop app" as the application type
   - Download the credentials JSON file
5. Save the downloaded file as `credentials.json` in the application directory

### 3. Configure the Application

Edit the `appsettings.json` file to customize your filter rules:

```json
{
  "FilterConfiguration": {
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2",
      "Temperature": 0.3,
      "MaxTokens": 500
    },
    "Gmail": {
      "CredentialsPath": "credentials.json",
      "FilteredLabel": "Filtered",
      "CheckIntervalSeconds": 60,
      "MaxMessagesPerCheck": 50
    },
    "FilterRules": [
      // Add your custom rules here
    ]
  }
}
```

### 4. Build and Run

```bash
dotnet build
dotnet run --project src/MostlyLucid.MailLLMFilter.App
```

## Configuration

### Filter Rules

Each filter rule can specify:

- **Name**: Identifier for the rule
- **Enabled**: Whether the rule is active
- **Keywords**: List of keywords to match (exact or partial)
- **Topics**: List of topics to match using LLM analysis
- **Mentions**: Specific phrases or names to look for
- **ConfidenceThreshold**: Minimum confidence (0.0-1.0) to trigger action
- **Action**: What to do with matched emails:
  - `MoveToFolder`: Move to specified label/folder
  - `Delete`: Move to trash
  - `MarkAsRead`: Mark as read
  - `Archive`: Remove from inbox
- **TargetFolder**: Label to move to (if Action is MoveToFolder)
- **AutoReplyTemplateId**: ID of auto-reply template to use
- **CustomPrompt**: Optional custom LLM prompt for this rule

### Auto-Reply Templates

Define templates for automatic responses:

```json
{
  "Id": "template-id",
  "Name": "Template Name",
  "Subject": "Re: {originalSubject}",
  "Body": "Hello {sender},\n\nYour message here...",
  "IncludeOriginal": false
}
```

Available placeholders:
- `{sender}`: Sender's name or email
- `{originalSubject}`: Original email subject

## Usage

### First Run

1. Launch the application
2. Click "Connect to Gmail"
3. A browser window will open for OAuth authentication
4. Grant the requested permissions
5. The application will now be connected to your Gmail account

### Processing Emails

- **Manual**: Click "Process Messages" to check for new unread emails
- **Automatic**: Enable "Auto-check" and set the interval (in seconds)

### Monitoring

- View processed emails in the main list
- Click on a result to see details including LLM analysis
- Toast notifications will appear for filtered messages

### Auto-Start

To make the application run automatically on Windows startup:

1. Press `Win + R` and type `shell:startup`
2. Create a shortcut to the application executable in this folder
3. Optionally configure the app to start minimized to system tray (feature can be added)

## Example Use Cases

### 1. Misdirected Emails

Filter emails meant for someone else (e.g., "Prof. Galloway"):

```json
{
  "Name": "Not For Me - Prof. Galloway",
  "Keywords": ["professor", "prof", "galloway"],
  "Mentions": ["Prof. Galloway", "Professor Galloway"],
  "ConfidenceThreshold": 0.7,
  "Action": "MoveToFolder",
  "TargetFolder": "NotForMe",
  "AutoReplyTemplateId": "wrong-person"
}
```

### 2. Spam Filtering

Aggressively filter spam and marketing:

```json
{
  "Name": "Spam Filter",
  "Keywords": ["unsubscribe", "click here", "limited time"],
  "Topics": ["marketing", "spam", "advertisement"],
  "ConfidenceThreshold": 0.8,
  "Action": "Delete"
}
```

### 3. Newsletter Management

Archive newsletters for later reading:

```json
{
  "Name": "Newsletters",
  "Keywords": ["newsletter", "digest"],
  "Topics": ["newsletter", "update"],
  "ConfidenceThreshold": 0.6,
  "Action": "Archive"
}
```

## Architecture

### Projects

- **MostlyLucid.MailLLMFilter.Core**: Core business logic
  - Models: Email messages, filter results, configurations
  - Services: Gmail API, Ollama LLM, Filter engine
  - Configuration: Settings and filter rules

- **MostlyLucid.MailLLMFilter.App**: WPF Windows application
  - ViewModels: MVVM pattern with CommunityToolkit.Mvvm
  - Views: WPF UI
  - Services: Notifications

### Key Components

1. **GmailService**: Handles Gmail API operations
   - Authentication via OAuth 2.0
   - Reading unread messages
   - Moving, deleting, archiving messages
   - Sending replies

2. **OllamaLlmService**: Integrates with Ollama for LLM analysis
   - Analyzes email content against filter rules
   - Returns confidence scores and reasoning
   - Detects topics and mentions

3. **FilterEngine**: Coordinates filtering process
   - Combines keyword matching with LLM analysis
   - Executes actions on matched messages
   - Triggers auto-replies

4. **NotificationService**: Windows toast notifications
   - Displays alerts for filtered messages
   - Shows batch processing results
   - Error notifications

## Customization

### Custom LLM Prompts

You can customize how the LLM analyzes emails by providing a `CustomPrompt` in your filter rule:

```json
{
  "Name": "Custom Analysis",
  "CustomPrompt": "Analyze this email and determine if it's related to technical support requests. Look for mentions of bugs, errors, or help requests. Be conservative and only match if you're very confident."
}
```

### Adjusting Confidence Thresholds

- **0.5-0.6**: Lenient filtering, may have false positives
- **0.7-0.8**: Balanced filtering (recommended)
- **0.9-1.0**: Strict filtering, only very obvious matches

### Multiple Filter Rules

Rules are evaluated in order. The first matching rule takes action, so order your rules from most specific to most general.

## Troubleshooting

### Gmail Authentication Issues

- Ensure `credentials.json` is in the correct location
- Check that the Gmail API is enabled in Google Cloud Console
- Try deleting the token file and re-authenticating (located in `%APPDATA%\MostlyLucid.MailLLMFilter`)

### Ollama Connection Issues

- Verify Ollama is running: `ollama list`
- Check the endpoint in `appsettings.json` matches your Ollama server
- Ensure the model is downloaded: `ollama pull llama3.2`

### No Emails Being Filtered

- Check that filter rules are enabled
- Lower the confidence threshold
- Review the LLM analysis in the details panel
- Check application logs for errors

## Privacy & Security

- All email processing happens locally on your machine
- OAuth tokens are stored securely in your user profile
- The LLM (Ollama) runs locally, no emails are sent to external services
- Gmail API access is limited to the scopes you approve

## Development

### Building

```bash
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Project Structure

```
├── src/
│   ├── MostlyLucid.MailLLMFilter.Core/
│   │   ├── Configuration/
│   │   ├── Models/
│   │   └── Services/
│   └── MostlyLucid.MailLLMFilter.App/
│       ├── Services/
│       ├── ViewModels/
│       └── Views/
├── appsettings.json
└── README.md
```

## License

See LICENSE file for details.

## Contributing

Contributions welcome! Please open an issue or submit a pull request.

## Support

For issues, questions, or feature requests, please open an issue on GitHub.

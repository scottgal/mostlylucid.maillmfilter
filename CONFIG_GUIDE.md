# Configuration Guide for MostlyLucid Mail LLM Filter

This guide provides comprehensive information on configuring the Mail LLM Filter, including common filter templates, prompt examples, and best practices.

## Table of Contents

- [Overview](#overview)
- [Configuration Structure](#configuration-structure)
- [LLM Filter Templates](#llm-filter-templates)
- [Common Use Cases](#common-use-cases)
- [Prompt Engineering Guide](#prompt-engineering-guide)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

The Mail LLM Filter uses a combination of keyword matching (30% weight) and LLM-powered semantic analysis (70% weight) to classify and filter emails. Configuration is done through `appsettings.json` using three main components:

1. **LLM Filter Templates**: Reusable analysis strategies for different email types
2. **Filter Rules**: Specific rules that apply templates to match emails
3. **Auto-Reply Templates**: Automated response templates

---

## Configuration Structure

### Basic Configuration Schema

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
    "LlmFilterTemplates": [ /* ... */ ],
    "FilterRules": [ /* ... */ ],
    "AutoReplyTemplates": [ /* ... */ ]
  }
}
```

### Configuration Options Explained

#### Ollama Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Endpoint` | string | `http://localhost:11434` | Ollama API endpoint |
| `Model` | string | `llama3.2` | Default LLM model |
| `Temperature` | float | `0.3` | LLM temperature (0.0-1.0, lower is more deterministic) |
| `MaxTokens` | int | `500` | Maximum tokens in LLM response |

**Temperature Guide:**
- `0.0-0.2`: Very deterministic, minimal creativity (best for strict filtering)
- `0.3-0.5`: Balanced consistency (recommended for most filters)
- `0.6-0.8`: More varied responses
- `0.9-1.0`: Maximum creativity (not recommended for filtering)

#### Gmail Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `CredentialsPath` | string | `credentials.json` | Path to OAuth credentials |
| `FilteredLabel` | string | `Filtered` | Default label for filtered messages |
| `CheckIntervalSeconds` | int | `60` | Auto-check interval |
| `MaxMessagesPerCheck` | int | `50` | Maximum emails per check |

---

## LLM Filter Templates

### Template Structure

An LLM Filter Template defines a reusable analysis strategy:

```json
{
  "Id": "unique-template-id",
  "Name": "Human-Readable Name",
  "Description": "What this template detects",
  "Model": "llama3.2",  // Optional: override global model
  "Temperature": 0.2,   // Optional: override global temperature
  "MaxTokens": 400,     // Optional: override global max tokens
  "SystemPrompt": "Role and context for the LLM",
  "PromptTemplate": "Analysis instructions with placeholders",
  "OutputFormat": "Expected response format",
  "Examples": [],       // Few-shot learning examples
  "RequiresKeywords": true,
  "RequiresTopics": true,
  "RequiresMentions": true
}
```

### Available Placeholders

Use these in `PromptTemplate`:

- `{from}`: Sender's name or email
- `{subject}`: Email subject line
- `{body}`: Email body (automatically truncated to fit context)
- `{keywords}`: Keywords from the filter rule (comma-separated)
- `{topics}`: Topics from the filter rule (comma-separated)
- `{mentions}`: Mentions from the filter rule (comma-separated)

### Pre-Configured Templates

The project includes several ready-to-use templates:

1. **academic-mistaken-identity**: Detects emails meant for professors or academics
2. **aggressive-spam-detector**: High-confidence spam and phishing detection
3. **newsletter-classifier**: Identifies newsletters and digests
4. **technical-support-detector**: Finds support requests and bug reports
5. **urgent-action-required**: Identifies time-sensitive emails

---

## Common Use Cases

### 1. Misdirected Academic Emails

**Scenario**: You share a name with a professor and receive their student emails.

**Template**:
```json
{
  "Id": "academic-mistaken-identity",
  "Name": "Academic Mistaken Identity Filter",
  "Temperature": 0.2,
  "SystemPrompt": "You are an expert email classifier specializing in identifying misdirected academic emails. Look for course numbers, office hours, assignments, or direct addressing to professors.",
  "PromptTemplate": "Analyze if this email was meant for someone else in academia.\n\nFrom: {from}\nSubject: {subject}\nBody: {body}\n\nMentions to check: {mentions}\nKeywords: {keywords}\n\nDoes this email contain:\n1. Direct addressing to someone else?\n2. Course/assignment references?\n3. Clear mistaken identity?\n\nBe conservative - only match with strong evidence.",
  "Examples": [
    {
      "Subject": "Question about CS101 Assignment",
      "Body": "Hi Prof. Galloway, I have a question about the midterm...",
      "ExpectedResult": "{\"match\": true, \"confidence\": 0.95}",
      "Explanation": "Direct addressing + course reference"
    }
  ]
}
```

**Filter Rule**:
```json
{
  "Name": "Prof. Galloway Misdirected Emails",
  "Enabled": true,
  "Keywords": ["professor", "prof", "galloway"],
  "Topics": ["academic", "university", "course", "assignment"],
  "Mentions": ["Prof. Galloway", "Professor Galloway"],
  "ConfidenceThreshold": 0.75,
  "Action": "MoveToFolder",
  "TargetFolder": "NotForMe/Academic",
  "AutoReplyTemplateId": "not-prof-galloway",
  "LlmFilterTemplateId": "academic-mistaken-identity"
}
```

### 2. Aggressive Spam Filter

**Scenario**: Block obvious spam, phishing, and scam emails.

**Template**:
```json
{
  "Id": "aggressive-spam-detector",
  "Name": "Aggressive Spam Detector",
  "Temperature": 0.1,
  "SystemPrompt": "You are a cybersecurity expert. Detect spam, phishing, and scams. Look for urgency tactics, suspicious links, too-good-to-be-true offers, and poor grammar.",
  "PromptTemplate": "Analyze for spam/phishing:\n\nFrom: {from}\nSubject: {subject}\nBody: {body}\n\nCheck for:\n- Urgency tactics (ACT NOW, LIMITED TIME)\n- Suspicious sender\n- Too-good-to-be-true claims\n- Requests for personal info\n- ALL CAPS or multiple !!!\n- Generic greetings\n\nFlagged keywords: {keywords}\n\nOnly mark as spam if >85% confident."
}
```

**Filter Rule**:
```json
{
  "Name": "Aggressive Spam Filter",
  "Enabled": true,
  "Keywords": [
    "unsubscribe", "click here", "act now", "limited time",
    "congratulations", "you've won", "free gift", "earn money",
    "weight loss", "miracle", "guarantee", "$$$"
  ],
  "Topics": ["marketing", "spam", "phishing", "scam"],
  "ConfidenceThreshold": 0.85,
  "Action": "Delete",
  "LlmFilterTemplateId": "aggressive-spam-detector"
}
```

### 3. Newsletter Organizer

**Scenario**: Automatically archive newsletters and digests.

**Template**:
```json
{
  "Id": "newsletter-classifier",
  "Name": "Newsletter Classifier",
  "Temperature": 0.25,
  "SystemPrompt": "You are an email classifier for newsletters and digests. Look for regular publications, multiple article links, and subscription management.",
  "PromptTemplate": "Is this a newsletter or digest?\n\nFrom: {from}\nSubject: {subject}\nBody: {body}\n\nLook for:\n- Newsletter formatting\n- Multiple article summaries\n- Regular publication indicators (Weekly, Monthly)\n- Unsubscribe links\n- Curated content\n\nKeywords: {keywords}"
}
```

**Filter Rule**:
```json
{
  "Name": "Newsletter Archive",
  "Enabled": true,
  "Keywords": ["newsletter", "digest", "weekly", "monthly", "roundup"],
  "Topics": ["newsletter", "digest", "update"],
  "ConfidenceThreshold": 0.6,
  "Action": "MoveToFolder",
  "TargetFolder": "Newsletters",
  "LlmFilterTemplateId": "newsletter-classifier"
}
```

### 4. Technical Support Detector

**Scenario**: Route support requests to a dedicated folder.

**Template**:
```json
{
  "Id": "technical-support-detector",
  "Name": "Technical Support Detector",
  "Temperature": 0.3,
  "SystemPrompt": "Identify technical support requests, bug reports, and help requests.",
  "PromptTemplate": "Is this a technical support request?\n\nFrom: {from}\nSubject: {subject}\nBody: {body}\n\nLook for:\n- Bug reports or error messages\n- Feature requests\n- How-to questions\n- System issues\n- Error codes or stack traces\n\nIs the sender asking for help?"
}
```

**Filter Rule**:
```json
{
  "Name": "Technical Support Requests",
  "Enabled": true,
  "Keywords": ["bug", "error", "issue", "help", "support", "problem", "broken"],
  "Topics": ["technical", "support", "bug", "feature request"],
  "ConfidenceThreshold": 0.7,
  "Action": "MoveToFolder",
  "TargetFolder": "Support/Incoming",
  "AutoReplyTemplateId": "support-auto-response",
  "LlmFilterTemplateId": "technical-support-detector"
}
```

### 5. Urgent/Priority Emails

**Scenario**: Highlight emails requiring immediate attention.

**Template**:
```json
{
  "Id": "urgent-action-required",
  "Name": "Urgent Action Required",
  "Temperature": 0.15,
  "SystemPrompt": "Identify emails requiring urgent action. Look for deadlines, urgency language, and time-sensitive matters.",
  "PromptTemplate": "Does this require urgent action?\n\nFrom: {from}\nSubject: {subject}\nBody: {body}\n\nLook for:\n- Urgency language (ASAP, urgent, critical)\n- Deadlines (today, EOD, by Friday)\n- Time-sensitive matters\n\nKeywords: {keywords}"
}
```

**Filter Rule**:
```json
{
  "Name": "Urgent Items",
  "Enabled": true,
  "Keywords": ["urgent", "asap", "immediately", "critical", "emergency", "deadline", "eod"],
  "Topics": ["urgent", "deadline", "time-sensitive"],
  "ConfidenceThreshold": 0.8,
  "Action": "MarkAsRead",
  "LlmFilterTemplateId": "urgent-action-required"
}
```

---

## Prompt Engineering Guide

### Effective Prompt Structure

A good LLM filter prompt has three parts:

1. **Context**: Set the LLM's role
   ```
   "You are an expert email classifier specializing in [domain]."
   ```

2. **Task**: Provide email details and analysis criteria
   ```
   "Analyze this email:
   From: {from}
   Subject: {subject}
   Body: {body}

   Check for: [specific criteria]"
   ```

3. **Output Format**: Specify expected response
   ```
   "Respond in JSON format:
   {
     \"match\": true/false,
     \"confidence\": 0.0-1.0,
     \"reason\": \"explanation\"
   }"
   ```

### Prompt Best Practices

✅ **DO:**
- Be specific about what to detect
- Include examples (few-shot learning)
- Set clear confidence thresholds
- Use conservative language ("only match if...")
- Specify edge cases to avoid

❌ **DON'T:**
- Use vague criteria ("interesting emails")
- Over-complicate prompts (keep under 500 words)
- Forget to specify output format
- Use high creativity (temperature > 0.5 for filtering)

### Advanced Prompt Techniques

#### 1. Few-Shot Learning

Include examples to guide the LLM:

```json
{
  "Examples": [
    {
      "Subject": "Meeting tomorrow",
      "Body": "Let's meet at 3pm",
      "ExpectedResult": "{\"match\": false, \"confidence\": 0.1}",
      "Explanation": "Normal meeting invite, not spam"
    },
    {
      "Subject": "URGENT: CLAIM YOUR PRIZE NOW!!!",
      "Body": "You've won $1,000,000!",
      "ExpectedResult": "{\"match\": true, \"confidence\": 0.98}",
      "Explanation": "Clear spam indicators"
    }
  ]
}
```

#### 2. Chain-of-Thought Prompting

Guide the LLM's reasoning:

```
"Analyze step-by-step:
1. Who is the sender?
2. Is the subject line suspicious?
3. Does the body contain urgency tactics?
4. Are there requests for personal information?
5. Overall assessment: spam or legitimate?"
```

#### 3. Confidence Calibration

Help the LLM assign appropriate confidence:

```
"Confidence guidelines:
- 0.9-1.0: Absolutely certain (multiple strong indicators)
- 0.7-0.9: Very confident (2-3 clear indicators)
- 0.5-0.7: Moderately confident (1-2 indicators)
- 0.0-0.5: Uncertain or doesn't match"
```

---

## Best Practices

### Configuration Best Practices

1. **Start Conservative**: Use higher confidence thresholds (0.8+) initially
2. **Test Incrementally**: Enable one rule at a time
3. **Monitor False Positives**: Review filtered emails regularly
4. **Order Rules Carefully**: Most specific rules first, general rules last
5. **Use Descriptive Names**: Make rule purposes clear

### Performance Optimization

1. **Keyword Optimization**: Include strong keywords for 30% boost
2. **Model Selection**: Use smaller models for simple filters
3. **Template Reuse**: Share templates across similar rules
4. **Batch Processing**: Increase `MaxMessagesPerCheck` for bulk processing
5. **Adjust Intervals**: Longer intervals reduce API calls

### Security Considerations

1. **Protect Credentials**: Never commit `credentials.json` to Git
2. **Review Auto-Replies**: Test templates before enabling
3. **Careful with Delete**: Use MoveToFolder instead when testing
4. **Rate Limiting**: Don't set `CheckIntervalSeconds` too low (<10)
5. **Monitor Logs**: Watch for suspicious patterns

---

## Troubleshooting

### Common Issues

#### Issue: Too Many False Positives

**Solutions:**
- Increase `ConfidenceThreshold` (try 0.85 or 0.9)
- Make prompt more specific
- Add negative examples
- Reduce `Temperature` for more consistent results

#### Issue: Missing Legitimate Matches

**Solutions:**
- Lower `ConfidenceThreshold` (try 0.6 or 0.65)
- Add more keywords
- Review and improve prompt
- Check if LLM model has enough context

#### Issue: Inconsistent Results

**Solutions:**
- Lower `Temperature` (0.1-0.2 for deterministic results)
- Add more few-shot examples
- Use more specific keywords
- Increase `MaxTokens` for detailed analysis

#### Issue: Performance Too Slow

**Solutions:**
- Use smaller model (e.g., `llama3.2:7b` instead of `:70b`)
- Reduce `MaxTokens` (try 300)
- Increase `CheckIntervalSeconds`
- Reduce `MaxMessagesPerCheck`

---

## Example Configurations

### Minimal Setup (One Simple Rule)

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
      "FilteredLabel": "Filtered"
    },
    "FilterRules": [
      {
        "Name": "Simple Spam Filter",
        "Enabled": true,
        "Keywords": ["spam", "unsubscribe"],
        "Topics": [],
        "Mentions": [],
        "ConfidenceThreshold": 0.8,
        "Action": "MoveToFolder",
        "TargetFolder": "Spam"
      }
    ]
  }
}
```

### Advanced Multi-Rule Setup

See `appsettings.json` for a complete example with:
- 5 LLM filter templates
- 5 filter rules
- 2 auto-reply templates
- All advanced features demonstrated

---

## Additional Resources

- **Main README**: Complete setup and architecture guide
- **appsettings.json**: Full configuration example
- **Source Code**: `/src/MostlyLucid.MailLLMFilter.Core/`

---

## Support

For issues, questions, or contributions:
- **GitHub Issues**: https://github.com/scottgal/mostlylucid.maillmfilter/issues
- **Discussions**: https://github.com/scottgal/mostlylucid.maillmfilter/discussions

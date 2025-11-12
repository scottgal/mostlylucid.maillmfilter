using Microsoft.Toolkit.Uwp.Notifications;
using MostlyLucid.MailLLMFilter.Core.Models;

namespace MostlyLucid.MailLLMFilter.App.Services;

/// <summary>
/// Service for displaying Windows toast notifications
/// </summary>
public class NotificationService
{
    private const string AppId = "MostlyLucid.MailLLMFilter";

    /// <summary>
    /// Show notification when a message is filtered
    /// </summary>
    public void ShowFilteredMessageNotification(FilterResult result)
    {
        var message = result.Message;
        var title = result.IsMatch ? "Email Filtered" : "Email Processed";

        var toastContent = new ToastContentBuilder()
            .AddText(title)
            .AddText($"From: {message.FromName ?? message.From}")
            .AddText($"Subject: {message.Subject}");

        if (result.IsMatch && result.MatchedRule != null)
        {
            toastContent.AddText($"Matched Rule: {result.MatchedRule.Name}");
            toastContent.AddText($"Confidence: {result.Confidence:P0}");
        }

        if (result.ActionTaken)
        {
            toastContent.AddText($"Action: {result.ActionDescription}");
        }

        toastContent
            .AddAudio(new ToastAudio() { Silent = false })
            .Show();
    }

    /// <summary>
    /// Show notification for batch processing results
    /// </summary>
    public void ShowBatchProcessedNotification(int total, int matched, int unmatched)
    {
        new ToastContentBuilder()
            .AddText("Batch Processing Complete")
            .AddText($"Total: {total} messages")
            .AddText($"Matched: {matched}, Unmatched: {unmatched}")
            .Show();
    }

    /// <summary>
    /// Show error notification
    /// </summary>
    public void ShowErrorNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .AddAudio(new ToastAudio() { Silent = false })
            .Show();
    }

    /// <summary>
    /// Show info notification
    /// </summary>
    public void ShowInfoNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }
}

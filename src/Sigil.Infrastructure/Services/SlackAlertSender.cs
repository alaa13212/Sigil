using System.Net.Http.Json;
using System.Text.Json;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Services;

internal class SlackAlertSender(HttpClient http) : IAlertSender
{
    public AlertChannelType Channel => AlertChannelType.Slack;

    public async Task<bool> SendAsync(AlertRule rule, Issue issue, string issueUrl)
    {
        var config = JsonSerializer.Deserialize<SlackConfig>(rule.AlertChannel!.Config);
        if (config?.WebhookUrl is null) return false;

        var triggerLabel = rule.Trigger switch
        {
            AlertTrigger.NewIssue         => "New Issue",
            AlertTrigger.IssueRegression  => "Issue Regression",
            AlertTrigger.ThresholdExceeded => "Threshold Exceeded",
            AlertTrigger.NewHighSeverity  => "New High-Severity Issue",
            _                             => rule.Trigger.ToString()
        };

        var severityEmoji = issue.Level switch
        {
            Severity.Fatal   => "🔴",
            Severity.Error   => "🟠",
            Severity.Warning => "🟡",
            _                => "⚪"
        };

        var payload = new
        {
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = $"{severityEmoji} Sigil Alert for ({issue.Project?.Name}): {triggerLabel}", emoji = true }
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*<{issueUrl}|{EscapeSlack(issue.Title ?? string.Empty)}>*" }
                },
                new
                {
                    type = "section",
                    fields = new[]
                    {
                        new { type = "mrkdwn", text = $"*Severity:*\n{issue.Level}" },
                        new { type = "mrkdwn", text = $"*Rule:*\n{EscapeSlack(rule.Name)}" },
                        new { type = "mrkdwn", text = $"*Occurrences:*\n{issue.OccurrenceCount:N0}" },
                        new { type = "mrkdwn", text = $"*First Seen:*\n{issue.FirstSeen:yyyy-MM-dd HH:mm} UTC" },
                    }
                },
                new
                {
                    type = "actions",
                    elements = new[]
                    {
                        new { type = "button", text = new { type = "plain_text", text = "View Issue" }, url = issueUrl, action_id = "view_issue" }
                    }
                }
            }
        };

        var response = await http.PostAsJsonAsync(config.WebhookUrl, payload);
        return response.IsSuccessStatusCode;
    }

    private static string EscapeSlack(string text) =>
        text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("|", "\u2502")
            .Replace("`", "\u2018")
            .Replace("*", "\u2217");

    private record SlackConfig(string? WebhookUrl);
}

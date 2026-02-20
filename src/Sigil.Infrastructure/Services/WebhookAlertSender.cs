using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Services;

internal class WebhookAlertSender(HttpClient http) : IAlertSender
{
    public AlertChannel Channel => AlertChannel.Webhook;

    public async Task<bool> SendAsync(AlertRule rule, Issue issue, string issueUrl)
    {
        var config = JsonSerializer.Deserialize<WebhookConfig>(rule.ChannelConfig);
        if (config?.Url is null) return false;

        var payload = new
        {
            trigger = rule.Trigger.ToString(),
            rule = new { id = rule.Id, name = rule.Name },
            issue = new
            {
                id = issue.Id,
                title = issue.Title,
                exception_type = issue.ExceptionType,
                severity = issue.Level.ToString().ToLower(),
                status = issue.Status.ToString().ToLower(),
                occurrence_count = issue.OccurrenceCount,
                first_seen = issue.FirstSeen,
                last_seen = issue.LastSeen,
                url = issueUrl
            },
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, config.Url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrEmpty(config.Secret))
        {
            var signature = ComputeHmacSha256(json, config.Secret);
            request.Headers.Add("X-Sigil-Signature", $"sha256={signature}");
        }

        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLower();
    }

    private record WebhookConfig(string? Url, string? Secret = null);
}

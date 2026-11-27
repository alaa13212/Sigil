using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class RawEnvelopeService(SigilDbContext context, IDateTime dateTime) : IRawEnvelopeService
{
    public async Task StoreAsync(int projectId, string rawData, DateTime receivedAt)
    {
        context.RawEnvelopes.Add(NewEnvelope(projectId, rawData, receivedAt));
        await context.SaveChangesAsync();
    }

    public async Task BulkStoreAsync(IEnumerable<(int ProjectId, string RawData, DateTime ReceivedAt)> items)
    {
        var entities = items.Select(i => NewEnvelope(i.ProjectId, i.RawData, i.ReceivedAt));
        context.RawEnvelopes.AddRange(entities);
        await context.SaveChangesAsync();
    }

    public async Task<List<RawEnvelope>> FetchUnprocessedAsync(int batchSize)
    {
        return await context.RawEnvelopes
            .Where(e => e.Error == null)
            .OrderBy(e => e.ReceivedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task DeleteAsync(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        await context.RawEnvelopes
            .Where(e => idList.Contains(e.Id))
            .ExecuteDeleteAsync();
    }

    public async Task<int> RetryFailedAsync(IEnumerable<long>? ids = null)
    {
        var idList = ids?.ToList();
        var query = context.RawEnvelopes.Where(e => e.Error != null);

        if (idList is { Count: > 0 })
            query = query.Where(e => idList.Contains(e.Id));

        return await query.ExecuteUpdateAsync(s => s
            .SetProperty(e => e.Error, (string?)null)
            .SetProperty(e => e.ProcessedAt, (DateTime?)null));
    }

    public async Task BulkMarkFailedAsync(IEnumerable<(long Id, string Error)> failures)
    {
        var failureList = failures.ToList();
        if (failureList.Count == 0) return;

        var ids = failureList.Select(f => f.Id).ToList();
        var errorById = failureList.ToDictionary(f => f.Id, f => f.Error);

        var entities = await context.RawEnvelopes
            .Where(e => ids.Contains(e.Id))
            .AsTracking()
            .ToListAsync();

        var now = dateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.ProcessedAt = now;
            entity.Error = errorById[entity.Id];
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Builds an envelope row, marking an envelope Sigil does not process as discarded.</summary>
    private static RawEnvelope NewEnvelope(int projectId, string rawData, DateTime receivedAt)
    {
        string? unsupportedType = EnvelopeItemTypes.DetectUnsupported(rawData);

        return new RawEnvelope
        {
            ProjectId = projectId,
            RawData = rawData,
            ReceivedAt = receivedAt,
            Error = unsupportedType is null ? null : EnvelopeItemTypes.UnsupportedError(unsupportedType),
            ProcessedAt = unsupportedType is null ? null : receivedAt,
        };
    }
}

/// <summary>Sentry envelope item types and the detection of the types Sigil does not process.</summary>
internal static class EnvelopeItemTypes
{
    /// <summary>Item type of an error event envelope.</summary>
    public const string Event = "event";

    /// <summary>Label used for item types outside the known set.</summary>
    public const string Other = "other";

    /// <summary>Prefix of the error recorded for a discarded envelope.</summary>
    public const string UnsupportedErrorPrefix = "unsupported envelope type: ";

    /// <summary>Item types Sigil processes.</summary>
    public static readonly IReadOnlyList<string> Supported = [Event];

    /// <summary>Known Sentry envelope item types.</summary>
    public static readonly IReadOnlyList<string> Known =
    [
        Event,
        "transaction",
        "client_report",
        "session",
        "attachment",
        "profile",
        "user_report",
        "feedback",
        "replay_event",
        "replay_recording",
        "check_in",
        "statsd",
        "metric_buckets",
        "otel_log",
        "span",
        "log",
    ];

    /// <summary>Item type of an envelope that Sigil does not process, or null when the envelope holds a supported item.</summary>
    public static string? DetectUnsupported(string rawEnvelope)
    {
        if (string.IsNullOrEmpty(rawEnvelope))
            return null;

        using var reader = new StringReader(rawEnvelope);

        // The first line holds the envelope header, the lines after it alternate item header and payload.
        if (reader.ReadLine() is null)
            return null;

        string? unsupported = null;
        bool hasSupported = false;

        while (reader.ReadLine() is { } itemHeaderLine)
        {
            string? type = ReadItemType(itemHeaderLine);

            // Skip the payload line of this item.
            reader.ReadLine();

            if (type is null)
                continue;

            if (IsSupported(type))
            {
                hasSupported = true;
                continue;
            }

            unsupported ??= type;
        }

        if (hasSupported)
            return null;

        return unsupported;
    }

    /// <summary>Maps an item type onto the known set, folding unknown types into one label.</summary>
    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return Other;

        string trimmed = type.Trim().ToLowerInvariant();
        return Known.Contains(trimmed) ? trimmed : Other;
    }

    /// <summary>Error recorded for an envelope Sigil does not process.</summary>
    public static string UnsupportedError(string type) => UnsupportedErrorPrefix + Normalize(type);

    private static bool IsSupported(string type) =>
        Supported.Contains(type.Trim().ToLowerInvariant());

    private static string? ReadItemType(string itemHeaderLine)
    {
        try
        {
            using var document = JsonDocument.Parse(itemHeaderLine);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
                return null;

            return typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

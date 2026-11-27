using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class RawEnvelopeService(SigilDbContext context, IDateTime dateTime, ISigilMetrics metrics) : IRawEnvelopeService
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
    private RawEnvelope NewEnvelope(int projectId, string rawData, DateTime receivedAt)
    {
        string? unsupportedType = EnvelopeItemTypes.DetectUnsupported(rawData);

        if (unsupportedType is not null)
            metrics.RecordEnvelopeDiscarded(EnvelopeItemTypes.Normalize(unsupportedType));

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
    /// <remarks>
    /// An envelope is only discarded when every item header is read exactly. An item header may declare
    /// the byte length of its payload, and that payload may contain line feeds, so the payload is skipped
    /// by its declared length rather than by line. An envelope that cannot be read exactly is left alone.
    /// </remarks>
    public static string? DetectUnsupported(string rawEnvelope)
    {
        if (string.IsNullOrEmpty(rawEnvelope))
            return null;

        int position = 0;

        // The first line holds the envelope header, the lines after it alternate item header and payload.
        if (!TryReadLine(rawEnvelope, ref position, out _))
            return null;

        string? unsupported = null;
        bool hasSupported = false;

        while (TryReadLine(rawEnvelope, ref position, out string? itemHeaderLine))
        {
            if (!TryReadItemHeader(itemHeaderLine, out string? type, out int? length))
                return null;

            if (!TrySkipPayload(rawEnvelope, ref position, length))
                return null;

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

    /// <summary>Reads the next line and moves the position past it.</summary>
    private static bool TryReadLine(string envelope, ref int position, [NotNullWhen(true)] out string? line)
    {
        if (position >= envelope.Length)
        {
            line = null;
            return false;
        }

        int end = envelope.IndexOf('\n', position);
        if (end < 0)
        {
            line = envelope[position..];
            position = envelope.Length;
            return true;
        }

        int lineEnd = end > position && envelope[end - 1] == '\r' ? end - 1 : end;
        line = envelope[position..lineEnd];
        position = end + 1;
        return true;
    }

    /// <summary>Reads the item type and the optional payload length from an item header.</summary>
    private static bool TryReadItemHeader(string itemHeaderLine, out string type, out int? length)
    {
        type = string.Empty;
        length = null;

        try
        {
            using var document = JsonDocument.Parse(itemHeaderLine);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
                return false;

            if (typeElement.ValueKind != JsonValueKind.String)
                return false;

            type = typeElement.GetString() ?? string.Empty;

            if (document.RootElement.TryGetProperty("length", out JsonElement lengthElement))
            {
                if (lengthElement.ValueKind != JsonValueKind.Number || !lengthElement.TryGetInt32(out int parsedLength))
                    return false;

                if (parsedLength < 0)
                    return false;

                length = parsedLength;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Moves the position past the payload of an item.</summary>
    private static bool TrySkipPayload(string envelope, ref int position, int? length)
    {
        if (length is null)
            return TryReadLine(envelope, ref position, out _);

        int end = position;
        int bytes = 0;

        while (end < envelope.Length && bytes < length.Value)
        {
            bytes += Utf8Size(envelope, end);
            end++;
        }

        if (bytes != length.Value)
            return false;

        // The payload is followed by a line break.
        if (end < envelope.Length && envelope[end] == '\r')
            end++;

        if (end < envelope.Length && envelope[end] == '\n')
            end++;

        position = end;
        return true;
    }

    /// <summary>Number of UTF-8 bytes the character at the position occupies.</summary>
    private static int Utf8Size(string text, int position)
    {
        char current = text[position];

        if (char.IsHighSurrogate(current) && position + 1 < text.Length && char.IsLowSurrogate(text[position + 1]))
            return 4;

        if (current < 0x80)
            return 1;

        if (current < 0x800)
            return 2;

        return 3;
    }
}

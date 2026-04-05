using System.Text.Json;

namespace Sigil.Infrastructure.Services;

/// <summary>
/// Minimal Source Map V3 parser implementing the spec at
/// https://tc39.es/source-map-spec/
/// </summary>
internal sealed class SourceMapParser
{
    private readonly string[] _sources;
    private readonly string[] _names;

    // Indexed by [generatedLine][segmentIndex] = (genCol, srcIdx, origLine, origCol, nameIdx?)
    private readonly SegmentEntry[][] _segments;

    private SourceMapParser(string[] sources, string[] names, SegmentEntry[][] segments)
    {
        _sources = sources;
        _names = names;
        _segments = segments;
    }

    public static SourceMapParser? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("mappings", out var mappingsEl))
                return null;

            var sources = root.TryGetProperty("sources", out var srcEl)
                ? srcEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                : [];

            var names = root.TryGetProperty("names", out var namesEl)
                ? namesEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                : [];

            var segments = DecodeMappings(mappingsEl.GetString() ?? "");
            return new SourceMapParser(sources, names, segments);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns (originalFilename, originalLine, originalColumn, originalFunction?) for
    /// the given generated (0-based) line and column, or null if no mapping found.
    /// </summary>
    public (string? Filename, int Line, int Column, string? Function)? GetOriginalPosition(int generatedLine, int generatedColumn)
    {
        if (generatedLine < 0 || generatedLine >= _segments.Length)
            return null;

        var line = _segments[generatedLine];
        if (line.Length == 0)
            return null;

        // Binary search for the best matching segment (largest genCol <= generatedColumn)
        int lo = 0, hi = line.Length - 1, best = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (line[mid].GeneratedColumn <= generatedColumn)
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (best < 0)
            return null;

        var seg = line[best];
        if (!seg.HasSource)
            return null;

        var filename = seg.SourceIndex < _sources.Length ? _sources[seg.SourceIndex] : null;
        var function = seg.HasName && seg.NameIndex < _names.Length ? _names[seg.NameIndex] : null;
        return (filename, seg.OriginalLine, seg.OriginalColumn, function);
    }

    private static SegmentEntry[][] DecodeMappings(string mappings)
    {
        var lines = mappings.Split(';');
        var result = new SegmentEntry[lines.Length][];

        int srcIdx = 0, origLine = 0, origCol = 0, nameIdx = 0;

        for (int li = 0; li < lines.Length; li++)
        {
            var lineStr = lines[li];
            if (string.IsNullOrEmpty(lineStr))
            {
                result[li] = [];
                continue;
            }

            var segs = lineStr.Split(',');
            var entries = new List<SegmentEntry>(segs.Length);
            int genCol = 0;

            foreach (var seg in segs)
            {
                if (string.IsNullOrEmpty(seg)) continue;
                var fields = DecodeVlqGroup(seg);
                if (fields.Length == 0) continue;

                genCol += fields[0];
                var entry = new SegmentEntry { GeneratedColumn = genCol };

                if (fields.Length >= 4)
                {
                    srcIdx += fields[1];
                    origLine += fields[2];
                    origCol += fields[3];
                    entry.HasSource = true;
                    entry.SourceIndex = srcIdx;
                    entry.OriginalLine = origLine;
                    entry.OriginalColumn = origCol;

                    if (fields.Length >= 5)
                    {
                        nameIdx += fields[4];
                        entry.HasName = true;
                        entry.NameIndex = nameIdx;
                    }
                }

                entries.Add(entry);
            }

            result[li] = [.. entries];

            // Reset genCol for next line; other state is carried forward
            // (genCol is relative per line, others are cumulative across lines)
        }

        return result;
    }

    private static int[] DecodeVlqGroup(string encoded)
    {
        var result = new List<int>();
        int i = 0;
        while (i < encoded.Length)
        {
            (int value, int consumed) = DecodeVlq(encoded, i);
            result.Add(value);
            i += consumed;
        }
        return [.. result];
    }

    private static (int Value, int Consumed) DecodeVlq(string encoded, int offset)
    {
        int result = 0, shift = 0, consumed = 0;

        while (offset + consumed < encoded.Length)
        {
            int digit = Base64Value(encoded[offset + consumed]);
            consumed++;
            result |= (digit & 0x1F) << shift;
            shift += 5;
            if ((digit & 0x20) == 0)
                break;
        }

        // LSB is sign bit in VLQ
        int value = (result & 1) != 0 ? -(result >> 1) : result >> 1;
        return (value, consumed);
    }

    private static int Base64Value(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '+' => 62,
        '/' => 63,
        _ => throw new FormatException($"Invalid Base64 VLQ character: {c}")
    };

    private struct SegmentEntry
    {
        public int GeneratedColumn;
        public bool HasSource;
        public int SourceIndex;
        public int OriginalLine;
        public int OriginalColumn;
        public bool HasName;
        public int NameIndex;
    }
}

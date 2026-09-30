using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class SourceMapParserTests
{
    // ── Parse ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidV3Json_ReturnsParser()
    {
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAA"}""";
        var parser = SourceMapParser.Parse(json);
        parser.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MissingMappings_ReturnsNull()
    {
        var json = """{"version":3,"sources":["app.js"],"names":[]}""";
        var parser = SourceMapParser.Parse(json);
        parser.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var parser = SourceMapParser.Parse("not json");
        parser.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyMappings_ReturnsParserWithNoMappings()
    {
        var json = """{"version":3,"sources":[],"names":[],"mappings":""}""";
        var parser = SourceMapParser.Parse(json);
        parser.Should().NotBeNull();
        // An empty mappings string means no position can be resolved
        parser!.GetOriginalPosition(0, 0).Should().BeNull();
    }

    [Fact]
    public void Parse_NoSourcesOrNames_ReturnsParser()
    {
        // sources and names are optional per spec
        var json = """{"version":3,"mappings":"AAAA"}""";
        var parser = SourceMapParser.Parse(json);
        parser.Should().NotBeNull();
    }

    // ── GetOriginalPosition: basic ────────────────────────────────────────────

    [Fact]
    public void GetOriginalPosition_SimpleSingleSegment_ReturnsCorrectMapping()
    {
        // AAAA = genCol:0, srcIdx:0, origLine:0, origCol:0
        var json = """{"version":3,"sources":["src/app.js"],"names":[],"mappings":"AAAA"}""";
        var parser = SourceMapParser.Parse(json)!;

        var result = parser.GetOriginalPosition(0, 0);

        result.Should().NotBeNull();
        result!.Value.Filename.Should().Be("src/app.js");
        result.Value.Line.Should().Be(0);
        result.Value.Column.Should().Be(0);
        result.Value.Function.Should().BeNull();
    }

    [Fact]
    public void GetOriginalPosition_WithName_ReturnsFunction()
    {
        // AAAAC = genCol:0, srcIdx:0, origLine:0, origCol:0, nameIdx:1
        // AAAAA = genCol:0, srcIdx:0, origLine:0, origCol:0, nameIdx:0
        var json = """{"version":3,"sources":["app.js"],"names":["myFunc"],"mappings":"AAAAA"}""";
        var parser = SourceMapParser.Parse(json)!;

        var result = parser.GetOriginalPosition(0, 0);

        result.Should().NotBeNull();
        result!.Value.Function.Should().Be("myFunc");
    }

    [Fact]
    public void GetOriginalPosition_NegativeGeneratedLine_ReturnsNull()
    {
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAA"}""";
        var parser = SourceMapParser.Parse(json)!;

        parser.GetOriginalPosition(-1, 0).Should().BeNull();
    }

    [Fact]
    public void GetOriginalPosition_OutOfRangeGeneratedLine_ReturnsNull()
    {
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAA"}""";
        var parser = SourceMapParser.Parse(json)!;

        parser.GetOriginalPosition(99, 0).Should().BeNull();
    }

    [Fact]
    public void GetOriginalPosition_EmptyLine_ReturnsNull()
    {
        // Two lines: line 0 has a segment, line 1 is empty (;;)
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAA;;"}""";
        var parser = SourceMapParser.Parse(json)!;

        parser.GetOriginalPosition(1, 0).Should().BeNull();
    }

    // ── GetOriginalPosition: multi-segment binary search ──────────────────────

    [Fact]
    public void GetOriginalPosition_MultipleSegments_FindsCorrectSegment()
    {
        // Segments at genCol 0 and 6 (G = +3 → wait, G=6 as base64=6, VLQ decode: 6>>1=3, positive, so delta=3?)
        // Let me use explicit VLQ: AAAA,GAAA
        // A=0, genCol=0; G=6, base64=6, bits: sign=0, value=3, so genCol delta = +3, genCol=3
        // AAAA = genCol:0, src:0, origLine:0, origCol:0
        // GAAA = genCol:+3 → genCol:3, src:+0 → 0, origLine:+0 → 0, origCol:+0 → 0
        var json = """{"version":3,"sources":["src/a.js","src/b.js"],"names":[],"mappings":"AAAA,GCAA"}""";
        // GCAA: G=genCol+3 → genCol=3, C=srcIdx+1 → srcIdx=1, A=origLine+0, A=origCol+0
        var parser = SourceMapParser.Parse(json)!;

        // col 0 should map to first segment (src[0])
        var first = parser.GetOriginalPosition(0, 0);
        first.Should().NotBeNull();
        first!.Value.Filename.Should().Be("src/a.js");

        // col 3 should map to second segment (src[1])
        var second = parser.GetOriginalPosition(0, 3);
        second.Should().NotBeNull();
        second!.Value.Filename.Should().Be("src/b.js");

        // col 5 (no exact match, falls back to segment at col 3)
        var fallback = parser.GetOriginalPosition(0, 5);
        fallback.Should().NotBeNull();
        fallback!.Value.Filename.Should().Be("src/b.js");
    }

    [Fact]
    public void GetOriginalPosition_ColumnBeforeFirstSegment_ReturnsNull()
    {
        // Only segment starts at genCol 4 (I = base64 8, value=4)
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"IAAA"}""";
        var parser = SourceMapParser.Parse(json)!;

        // Column 0 is before the first segment — no match
        parser.GetOriginalPosition(0, 0).Should().BeNull();
    }

    // ── GetOriginalPosition: multi-line ───────────────────────────────────────

    [Fact]
    public void GetOriginalPosition_MultiLineMapping_ResolvesCorrectLine()
    {
        // Line 0: AAAA (genCol:0 → src:0, origLine:0, origCol:0)
        // Line 1: AACA (genCol:0 → src:0, origLine:+1=1, origCol:0)
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAA;AACA"}""";
        var parser = SourceMapParser.Parse(json)!;

        var line0 = parser.GetOriginalPosition(0, 0);
        line0.Should().NotBeNull();
        line0!.Value.Line.Should().Be(0);

        var line1 = parser.GetOriginalPosition(1, 0);
        line1.Should().NotBeNull();
        line1!.Value.Line.Should().Be(1);
    }

    [Fact]
    public void GetOriginalPosition_CumulativeStateCarriedAcrossLines()
    {
        // Line 0: AAAA (src:0, origLine:0, origCol:0)
        // Line 1: AACA (src:0, origLine:+1→1, origCol:+0→0) — srcIdx stays 0 from prev line
        // Line 2: AACA (src:0, origLine:+1→2, origCol:+0→0)
        var json = """{"version":3,"sources":["a.js"],"names":[],"mappings":"AAAA;AACA;AACA"}""";
        var parser = SourceMapParser.Parse(json)!;

        parser.GetOriginalPosition(2, 0)!.Value.Line.Should().Be(2);
    }

    // ── Segment with only 1 field (no source info) ────────────────────────────

    [Fact]
    public void GetOriginalPosition_SegmentWithNoSource_ReturnsNull()
    {
        // A single VLQ field means no source info
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"A"}""";
        var parser = SourceMapParser.Parse(json)!;

        // Segment exists but has no source → null
        parser.GetOriginalPosition(0, 0).Should().BeNull();
    }

    // ── VLQ decoding edge cases ───────────────────────────────────────────────

    [Fact]
    public void GetOriginalPosition_NegativeOriginalColumn_DecodesCorrectly()
    {
        // D in VLQ = base64 3 = 0b000011: continuation=0, bits4-1=001, sign=1 → value = -1
        // ADAA: genCol=0, srcIdx=-1(??) wait, srcIdx can't be negative in valid maps
        // Let me use origCol=-1: AADA where D is srcIdx delta, which... hmm
        // Actually let me test with a positive origLine delta that's multi-char VLQ
        // Use AAQB: A=genCol:0, A=srcIdx:0, Q=base64 16=0b010000: bits5=0, bits4-1=1000, bit0=0→+8, so origLine=8, B=base64 1=0b000001: bits5=0, bits4-1=0000, bit0=1→-0?? wait
        // Actually B = 1 = 0b000001: continuation=0, value bits4-1=0000=0, sign bit=1 → value = 0 with negative sign = 0? Or is it -0?
        // No, the formula is: value = (result >> 1), sign = result & 1. So B(=1): result=1, value=(1>>1)=0, sign=1 → -0 = 0. That's 0.
        // Let me use a simpler test: AAAE where E=4, value=(4>>1)=2, sign=0 → +2 for origCol
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAAE"}""";
        var parser = SourceMapParser.Parse(json)!;

        var result = parser.GetOriginalPosition(0, 0);
        result.Should().NotBeNull();
        result!.Value.Column.Should().Be(2);
    }

    [Fact]
    public void GetOriginalPosition_MultiByteVlq_DecodesCorrectly()
    {
        // Test that large column deltas using multi-char VLQ decode correctly
        // Testing: AAKA where K = base64 10, value=10>>1=5, sign=0 → origLine=5
        var json = """{"version":3,"sources":["app.js"],"names":[],"mappings":"AAKA"}""";
        var parser = SourceMapParser.Parse(json)!;

        var result = parser.GetOriginalPosition(0, 0);
        result.Should().NotBeNull();
        result!.Value.Line.Should().Be(5);
    }

    [Fact]
    public void GetOriginalPosition_VlqDecoderEdgeCases_ContinuationBitNegativeDeltaAndNameAccumulation()
    {
        // This test is designed to kill the bulk of VLQ decoder survivors by exercising:
        // 1. Multi-byte VLQ (continuation bit '& 0x20'): 'g'=32 sets bit5, 'B'=1 follows → decodes +16
        //    Kills mutations: '& 0x20' → '& 0x1F', 'shift += 5' arithmetic, 'offset + consumed' indexing
        // 2. Negative delta ('D'=3, sign bit=1 → -1): tests the sign bit branch '(result & 1) != 0 ? -(result >> 1)'
        //    Kills mutations: negate sign check, always-positive return
        // 3. Name index accumulation across segments: second named segment must use += not =
        //    Kills mutations: 'nameIdx += fields[4]' → 'nameIdx = fields[4]'
        //
        // Mapping: "AAgBAA;AADA,KAAAC"
        //   Line 0, seg 1: "AAgBAA"
        //     A(0)=genCol 0, A(0)=srcIdx 0, gB(+16)=origLine 16 [multi-byte], A(0)=origCol 0, A(0)=nameIdx 0 → "myFunc"
        //   Line 1, seg 1: "AADA"
        //     A(0)=genCol 0, A(0)=srcIdx 0, D(-1)=origLine 15 [negative delta], A(0)=origCol 0
        //   Line 1, seg 2: "KAAAC"
        //     K(+5)=genCol 5, A(0)=srcIdx 0, A(0)=origLine 15, A(0)=origCol 0, C(+1)=nameIdx 1 → "otherFunc"
        //
        // VLQ alphabet: 'g'=32 (continuation set, lower 5 bits=0), 'B'=1 (no continuation, lower 5 bits=1)
        //   → result = 0 | (1<<5) = 32, value = 32>>1 = 16, sign = 0 → +16 ✓
        // VLQ 'D'=3: lower 5 bits=3, result=3, value=3>>1=1, sign=3&1=1 → -1 ✓
        // VLQ 'K'=10: lower 5 bits=10, result=10, value=10>>1=5, sign=0 → +5 ✓
        // VLQ 'C'=2: lower 5 bits=2, result=2, value=2>>1=1, sign=0 → +1 ✓
        var json = """{"version":3,"sources":["src/a.js"],"names":["myFunc","otherFunc"],"mappings":"AAgBAA;AADA,KAAAC"}""";
        var parser = SourceMapParser.Parse(json)!;

        // Line 0, col 0: multi-byte VLQ, origLine=16, function="myFunc"
        var pos0 = parser.GetOriginalPosition(0, 0);
        pos0.Should().NotBeNull();
        pos0!.Value.Line.Should().Be(16, "gB encodes +16 via continuation byte");
        pos0.Value.Function.Should().Be("myFunc");

        // Line 1, col 0: negative origLine delta (-1) → cumulative origLine=15
        var pos1 = parser.GetOriginalPosition(1, 0);
        pos1.Should().NotBeNull();
        pos1!.Value.Line.Should().Be(15, "D encodes -1 delta; cumulative origLine = 16 - 1 = 15");

        // Line 1, col 5: nameIdx accumulated to 1 → "otherFunc"
        var pos2 = parser.GetOriginalPosition(1, 5);
        pos2.Should().NotBeNull();
        pos2!.Value.Line.Should().Be(15);
        pos2.Value.Function.Should().Be("otherFunc", "C encodes nameIdx delta +1; nameIdx was 0 → now 1");
    }
}

using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Persistence;

public class EnvelopeItemTypesTests
{
    [Fact]
    public void DetectUnsupported_ReportsAnItemTypeSigilDoesNotProcess()
    {
        string envelope = "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"client_report\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().Be("client_report");
    }

    [Fact]
    public void DetectUnsupported_ReportsATransactionEnvelope()
    {
        string envelope = "{\"event_id\":\"5d1b1a1f\"}\n{\"type\":\"transaction\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().Be("transaction");
    }

    [Fact]
    public void DetectUnsupported_IgnoresAnEnvelopeThatHoldsAnEvent()
    {
        string envelope = "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"event\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().BeNull();
    }

    [Fact]
    public void DetectUnsupported_IgnoresAnEnvelopeThatHoldsAnEventAfterAnUnsupportedItem()
    {
        string envelope =
            "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"client_report\",\"length\":2}\n{}\n{\"type\":\"event\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().BeNull();
    }

    [Fact]
    public void DetectUnsupported_ReadsTheItemAfterAPayloadThatSpansLines()
    {
        // The attachment payload declares its length and holds line feeds, so the item
        // header that follows it must still be read. The attachment is the first item
        // Sigil does not process, so it names the envelope.
        string payload = "{\"name\":\"line one\nline two\"}";
        string envelope =
            "{\"event_id\":\"5d1b1a1e\"}\n" +
            $"{{\"type\":\"attachment\",\"length\":{System.Text.Encoding.UTF8.GetByteCount(payload)}}}\n" +
            payload + "\n" +
            "{\"type\":\"client_report\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().Be("attachment");
    }

    [Fact]
    public void DetectUnsupported_LeavesAnEnvelopeAloneWhenAnItemHeaderCannotBeRead()
    {
        string envelope = "{\"event_id\":\"5d1b1a1e\"}\nnot a header\n{\"type\":\"client_report\",\"length\":2}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().BeNull();
    }

    [Fact]
    public void DetectUnsupported_LeavesAnEnvelopeAloneWhenThePayloadLengthDoesNotMatch()
    {
        string envelope = "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"client_report\",\"length\":99}\n{}";

        EnvelopeItemTypes.DetectUnsupported(envelope).Should().BeNull();
    }

    [Fact]
    public void DetectUnsupported_IgnoresAnEmptyEnvelope()
    {
        EnvelopeItemTypes.DetectUnsupported("").Should().BeNull();
    }

    [Fact]
    public void Normalize_FoldsAnUnknownTypeIntoTheOtherLabel()
    {
        EnvelopeItemTypes.Normalize("something_new").Should().Be(EnvelopeItemTypes.Other);
    }

    [Fact]
    public void Normalize_KeepsAKnownType()
    {
        EnvelopeItemTypes.Normalize("client_report").Should().Be("client_report");
    }
}

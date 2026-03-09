using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class RawEnvelopeServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);
    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static RawEnvelopeService Create(SigilDbContext ctx) => new(ctx, StubDateTime());

    private async Task<RawEnvelope> InsertEnvelopeAsync(SigilDbContext ctx, int projectId,
        string rawData = "{}", DateTime? receivedAt = null, string? error = null, DateTime? processedAt = null)
    {
        var envelope = new RawEnvelope
        {
            ProjectId = projectId,
            RawData = rawData,
            ReceivedAt = receivedAt ?? DateTime.UtcNow,
            Error = error,
            ProcessedAt = processedAt,
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();
        return envelope;
    }

    // ── Store ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Store_PersistsSingleEnvelope()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var receivedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Create(ctx).StoreAsync(project.Id, "{\"test\":1}", receivedAt);

        await using var verify = Ctx();
        var saved = await verify.RawEnvelopes
            .Where(e => e.ProjectId == project.Id)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.RawData.Should().Be("{\"test\":1}");
        saved.ReceivedAt.Should().Be(receivedAt);
    }

    // ── BulkStore ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkStore_PersistsAllEnvelopes()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = DateTime.UtcNow;
        var items = new[]
        {
            (project.Id, "{\"a\":1}", now.AddSeconds(-2)),
            (project.Id, "{\"b\":2}", now.AddSeconds(-1)),
            (project.Id, "{\"c\":3}", now),
        };

        await Create(ctx).BulkStoreAsync(items);

        await using var verify = Ctx();
        var saved = await verify.RawEnvelopes
            .Where(e => e.ProjectId == project.Id)
            .OrderBy(e => e.ReceivedAt)
            .ToListAsync();
        saved.Should().HaveCount(3);
        saved.Select(e => e.RawData).Should().BeEquivalentTo(["{\"a\":1}", "{\"b\":2}", "{\"c\":3}"]);
    }

    // ── FetchUnprocessed ──────────────────────────────────────────────────────

    [Fact]
    public async Task FetchUnprocessed_ReturnsOnlyUnprocessed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = DateTime.UtcNow;
        await InsertEnvelopeAsync(ctx, project.Id, "{\"ok\":1}", now.AddSeconds(-2));
        await InsertEnvelopeAsync(ctx, project.Id, "{\"fail\":1}", now.AddSeconds(-1), error: "Parse error");

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).FetchUnprocessedAsync(10);

        result.Should().OnlyContain(e => e.Error == null);
    }

    [Fact]
    public async Task FetchUnprocessed_RespectsOrderAndBatchSize()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = DateTime.UtcNow;
        for (int i = 5; i >= 1; i--)
            await InsertEnvelopeAsync(ctx, project.Id, $"{{\"i\":{i}}}", now.AddSeconds(-i * 10));

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).FetchUnprocessedAsync(3);

        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(e => e.ReceivedAt);
    }

    [Fact]
    public async Task FetchUnprocessed_IgnoresAlreadyFailed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var failedEnvelope = await InsertEnvelopeAsync(ctx, project.Id, "{}", error: "err");

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).FetchUnprocessedAsync(10000);

        result.Should().NotContain(e => e.Id == failedEnvelope.Id);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesEnvelopes()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var e1 = await InsertEnvelopeAsync(ctx, project.Id);
        var e2 = await InsertEnvelopeAsync(ctx, project.Id);
        var e3 = await InsertEnvelopeAsync(ctx, project.Id);

        await using var ctx2 = Ctx();
        await Create(ctx2).DeleteAsync([e1.Id, e2.Id]);

        await using var verify = Ctx();
        verify.RawEnvelopes.Any(e => e.Id == e1.Id).Should().BeFalse();
        verify.RawEnvelopes.Any(e => e.Id == e2.Id).Should().BeFalse();
        verify.RawEnvelopes.Any(e => e.Id == e3.Id).Should().BeTrue("e3 was not deleted");
    }

    // ── BulkMarkFailed ────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkMarkFailed_SetsErrorAndProcessedAt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var envelope = await InsertEnvelopeAsync(ctx, project.Id);

        await using var ctx2 = Ctx();
        await Create(ctx2).BulkMarkFailedAsync([(envelope.Id, "Parse error")]);

        await using var verify = Ctx();
        var updated = await verify.RawEnvelopes.FindAsync(envelope.Id);
        updated!.Error.Should().Be("Parse error");
        updated.ProcessedAt.Should().NotBeNull();
    }

    // ── RetryFailed ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryFailed_ClearsErrorAndProcessedAt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var envelope = await InsertEnvelopeAsync(ctx, project.Id, error: "old error", processedAt: DateTime.UtcNow.AddHours(-1));

        // Retry only this specific envelope to avoid affecting other test data
        await using var ctx2 = Ctx();
        var count = await Create(ctx2).RetryFailedAsync([envelope.Id]);

        count.Should().Be(1);
        await using var verify = Ctx();
        var updated = await verify.RawEnvelopes.FindAsync(envelope.Id);
        updated!.Error.Should().BeNull();
        updated.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task RetryFailed_WithSpecificIds_OnlyRetriesThose()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var e1 = await InsertEnvelopeAsync(ctx, project.Id, error: "err1");
        var e2 = await InsertEnvelopeAsync(ctx, project.Id, error: "err2");

        await using var ctx2 = Ctx();
        await Create(ctx2).RetryFailedAsync([e1.Id]);

        await using var verify = Ctx();
        var updated1 = await verify.RawEnvelopes.FindAsync(e1.Id);
        var updated2 = await verify.RawEnvelopes.FindAsync(e2.Id);
        updated1!.Error.Should().BeNull("e1 was retried");
        updated2!.Error.Should().Be("err2", "e2 was not retried");
    }
}

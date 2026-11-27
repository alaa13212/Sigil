using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;
using Sigil.Infrastructure.Tests.Persistence;
using Sigil.Infrastructure.Workers;

namespace Sigil.Infrastructure.Tests.Workers;

[Collection(DbCollection)]
public class DigestionWorkerEnvelopeTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private class FixedDateTime(DateTime utcNow) : IDateTime
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    /// <summary>Invokes the private batch processing of the worker.</summary>
    private static async Task ProcessBatchAsync(DigestionWorker worker, List<RawEnvelope> batch, CancellationToken ct)
    {
        MethodInfo method = typeof(DigestionWorker)
            .GetMethod("ProcessBatch", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(DigestionWorker), "ProcessBatch");

        await (Task)method.Invoke(worker, [ct, batch])!;
    }

    private (DigestionWorker Worker, IEventParser Parser) CreateWorker(SigilDbContext db)
    {
        var parser = Substitute.For<IEventParser>();

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped<IRawEnvelopeService>(sp =>
            new RawEnvelopeService(sp.GetRequiredService<SigilDbContext>(), new FixedDateTime(DateTime.UtcNow)));
        services.AddScoped<IEventParsingContextBuilder>(_ => Substitute.For<IEventParsingContextBuilder>());
        services.AddSingleton(parser);
        services.AddScoped<IDigestionService>(_ => Substitute.For<IDigestionService>());

        var provider = services.BuildServiceProvider();

        var config = new BatchWorkersConfig
        {
            ["DigestionWorker"] = new BatchWorkerOptions { BatchSize = 50, FlushTimeout = TimeSpan.FromSeconds(1) }
        };

        var worker = new DigestionWorker(
            provider,
            Substitute.For<IDigestionSignal>(),
            Options.Create(config),
            new FixedDateTime(DateTime.UtcNow),
            new SigilMetrics(),
            NullLogger<DigestionWorker>.Instance);

        return (worker, parser);
    }

    [Fact]
    public async Task ProcessBatch_DiscardsAnEnvelopeWithAnUnsupportedItemType()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"client_report\",\"length\":2}\n{}",
            ReceivedAt = DateTime.UtcNow,
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        var (worker, parser) = CreateWorker(ctx);
        await ProcessBatchAsync(worker, [envelope], CancellationToken.None);

        await using var verify = Ctx();
        var stored = await verify.RawEnvelopes.FindAsync(envelope.Id);

        stored!.Error.Should().Be("unsupported envelope type: client_report");
        stored.ProcessedAt.Should().NotBeNull();
        parser.ReceivedCalls().Should().BeEmpty("an unsupported envelope is never parsed");
    }

    [Fact]
    public async Task ProcessBatch_DiscardsAnEnvelopeWithATransactionItemType()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{\"event_id\":\"5d1b1a1f\"}\n{\"type\":\"transaction\",\"length\":2}\n{}",
            ReceivedAt = DateTime.UtcNow,
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        var (worker, _) = CreateWorker(ctx);
        await ProcessBatchAsync(worker, [envelope], CancellationToken.None);

        await using var verify = Ctx();
        var stored = await verify.RawEnvelopes.FindAsync(envelope.Id);

        stored!.Error.Should().Be("unsupported envelope type: transaction");
    }

    [Fact]
    public async Task ProcessBatch_DoesNotDiscardAnEnvelopeThatCarriesAnEvent()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{\"event_id\":\"5d1b1a20\"}\n{\"type\":\"event\",\"length\":2}\n{}",
            ReceivedAt = DateTime.UtcNow,
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        var (worker, parser) = CreateWorker(ctx);
        parser.Parse(Arg.Any<EventParsingContext>(), Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(new List<ParsedEvent>()));

        await ProcessBatchAsync(worker, [envelope], CancellationToken.None);

        parser.ReceivedCalls().Should().NotBeEmpty("an event envelope is parsed");

        await using var verify = Ctx();
        var stored = await verify.RawEnvelopes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == envelope.Id);

        stored?.Error.Should().BeNull("an envelope with an event is not discarded for its item type");
    }
}

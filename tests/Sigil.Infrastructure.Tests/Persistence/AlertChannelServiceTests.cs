using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class AlertChannelServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new SigilDbContext(options);
    }

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    [Fact]
    public async Task CreateChannel_PersistsAndReturnsResponse()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());
        var request = new CreateAlertChannelRequest("Test Slack", AlertChannelType.Slack, "{\"url\":\"https://hooks.slack.com/test\"}");

        var result = await service.CreateChannelAsync(request);

        result.Name.Should().Be("Test Slack");
        result.Type.Should().Be(AlertChannelType.Slack);
        result.Id.Should().BeGreaterThan(0);

        // Verify it's in the database
        await using var verifyContext = CreateContext();
        var inDb = await verifyContext.AlertChannels.FindAsync(result.Id);
        inDb.Should().NotBeNull();
        inDb.Name.Should().Be("Test Slack");
    }

    [Fact]
    public async Task GetAllChannels_ReturnsOrderedByName()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());

        var suffix = Guid.NewGuid().ToString("N")[..8];
        await service.CreateChannelAsync(new($"Z-Order-{suffix}", AlertChannelType.Webhook, "{}"));
        await service.CreateChannelAsync(new($"A-Order-{suffix}", AlertChannelType.Slack, "{}"));

        var all = await service.GetAllChannelsAsync();
        var relevant = all.Where(c => c.Name.EndsWith(suffix)).Select(c => c.Name).ToList();

        relevant.Should().HaveCount(2);
        relevant.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task UpdateChannel_ModifiesExistingChannel()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());
        var created = await service.CreateChannelAsync(new("Original", AlertChannelType.Slack, "{}"));

        var updated = await service.UpdateChannelAsync(created.Id,
            new UpdateAlertChannelRequest("Renamed", AlertChannelType.Webhook, "{\"url\":\"new\"}"));

        updated.Should().NotBeNull();
        updated.Name.Should().Be("Renamed");
        updated.Type.Should().Be(AlertChannelType.Webhook);
    }

    [Fact]
    public async Task UpdateChannel_NonExistentId_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());

        var result = await service.UpdateChannelAsync(999999,
            new UpdateAlertChannelRequest("Name", AlertChannelType.Slack, "{}"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteChannel_RemovesFromDatabase()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());
        var created = await service.CreateChannelAsync(new("ToDelete", AlertChannelType.Slack, "{}"));

        var deleted = await service.DeleteChannelAsync(created.Id);

        deleted.Should().BeTrue();

        await using var verifyContext = CreateContext();
        var inDb = await verifyContext.AlertChannels.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteChannel_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = new AlertChannelService(context, StubDateTime());

        var deleted = await service.DeleteChannelAsync(999999);

        deleted.Should().BeFalse();
    }
}

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.NormalizationRules;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class NormalizationRuleServiceTests(TestDatabaseFixture fixture)
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

    private static INormalizationRuleCache StubCache()
    {
        var cache = Substitute.For<INormalizationRuleCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<List<TextNormalizationRule>?>()).Returns(false);
        return cache;
    }

    private async Task<int> CreateTestProjectAsync()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Name = $"NormRuleTest-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task CreateRule_PersistsAndReturnsEntity()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var rule = await service.CreateRuleAsync(projectId, new CreateNormalizationRuleRequest(
            @"\d+", "<NUM>", Priority: 100, Enabled: true, Description: "Numbers"));

        rule.Id.Should().BeGreaterThan(0);
        rule.Pattern.Should().Be(@"\d+");
        rule.Replacement.Should().Be("<NUM>");
        rule.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task GetRules_ReturnsOrderedByPriorityDescending()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: "Low"));
        await service.CreateRuleAsync(projectId, new(@"\w+", "<WORD>", Priority: 100, Enabled: true, Description: "High"));

        var rules = await service.GetRulesAsync(projectId);

        rules.Should().HaveCountGreaterOrEqualTo(2);
        rules.First().Priority.Should().BeGreaterThanOrEqualTo(rules.Last().Priority);
    }

    [Fact]
    public async Task UpdateRule_ModifiesExistingRule()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: "Original"));

        var updated = await service.UpdateRuleAsync(projectId, created.Id,
            new UpdateNormalizationRuleRequest(@"\d{3,}", "<BIGNUM>", Priority: 20, Enabled: false, Description: "Updated"));

        updated.Should().NotBeNull();
        updated.Pattern.Should().Be(@"\d{3,}");
        updated.Replacement.Should().Be("<BIGNUM>");
        updated.Priority.Should().Be(20);
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRule_NonExistentId_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var result = await service.UpdateRuleAsync(0, 999999,
            new UpdateNormalizationRuleRequest("x", "y", 0, true, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_RemovesFromDatabase()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));

        var deleted = await service.DeleteRuleAsync(projectId, created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.TextNormalizationRules.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var deleted = await service.DeleteRuleAsync(0, 999999);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());

        await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task UpdateRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));
        cache.ClearReceivedCalls();

        await service.UpdateRuleAsync(projectId, created.Id, new(@"\w+", "<WORD>", Priority: 20, Enabled: false, Description: null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task DeleteRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));
        cache.ClearReceivedCalls();

        await service.DeleteRuleAsync(projectId, created.Id);

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task CreateDefaultRulesPreset_ReturnsAllPresetsWithValidFields()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var rules = service.CreateDefaultRulesPreset();

        // Every preset must have a non-empty pattern, replacement, and description
        rules.Should().NotBeEmpty();
        rules.Should().AllSatisfy(r =>
        {
            r.Pattern.Should().NotBeEmpty("pattern must be set for every preset");
            r.Replacement.Should().NotBeEmpty("replacement must be set for every preset");
            r.Description.Should().NotBeEmpty("description must be set for every preset");
            r.Enabled.Should().BeTrue();
        });
        rules.Select(r => r.Description).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateDefaultRulesPreset_PatternsMatchIntendedInputs()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var rules = service.CreateDefaultRulesPreset().ToDictionary(r => r.Description!);

        // Each pattern must match its intended sample input and produce the correct replacement token
        AssertReplaces(rules, "IP Addresses",  "192.168.1.1",                            "{ip}");
        AssertReplaces(rules, "UUIDs",         "550e8400-e29b-41d4-a716-446655440000",   "{uuid}");
        AssertReplaces(rules, "Dates",         "2024-01-15",                              "{datetime}");
        AssertReplaces(rules, "Emails",        "user@example.com",                        "{email}");
        AssertReplaces(rules, "URLs",          "https://example.com/path",                "{url}");
        AssertReplaces(rules, "Boolean Values","true",                                    "{bool}");
        AssertReplaces(rules, "Numbers",       "42",                                      "{int}");
        AssertReplaces(rules, "Hexadecimal Numbers", "deadbeef",                          "{hex}");
        AssertReplaces(rules, "Epoch seconds", "1700000000",                              "{epoch}");
        AssertReplaces(rules, "Epoch millis",  "1700000000000",                           "{epochms}");
    }

    [Fact]
    public async Task GetRawRulesAsync_FiltersToProjectAndPopulatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var otherProjectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());

        // Create rule for the target project and one for another project
        await service.CreateRuleAsync(projectId,      new(@"\d+", "<NUM>", 10, true, "Target"));
        await service.CreateRuleAsync(otherProjectId, new(@"\w+", "<WORD>", 10, true, "Other"));
        cache.ClearReceivedCalls();

        var result = await service.GetRawRulesAsync(projectId);

        // Must return only rules for the given project
        result.Should().OnlyContain(r => r.ProjectId == projectId);
        // Cache must be populated after a DB miss
        cache.Received(1).Set(projectId, Arg.Any<List<TextNormalizationRule>>());
    }

    private static void AssertReplaces(Dictionary<string, TextNormalizationRule> rules, string description, string input, string expectedToken)
    {
        rules.Should().ContainKey(description);
        var rule = rules[description];
        var output = Regex.Replace(input, rule.Pattern, rule.Replacement);
        output.Should().Contain(expectedToken, $"pattern '{rule.Pattern}' with replacement '{rule.Replacement}' should produce token in output for input '{input}'");
    }
}

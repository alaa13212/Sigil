using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Persistence;

internal static class TestHelper
{
    private static readonly ConcurrentDictionary<string, NpgsqlDataSource> DataSources = new();

    public static SigilDbContext CreateContext(string connectionString)
    {
        var dataSource = DataSources.GetOrAdd(connectionString, cs =>
        {
            var builder = new NpgsqlDataSourceBuilder(cs);
            builder.EnableDynamicJson();
            return builder.Build();
        });
        var options = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(dataSource)
            .Options;
        return new SigilDbContext(options);
    }

    public static async Task<Project> CreateProjectAsync(SigilDbContext context, string? name = null)
    {
        var project = new Project
        {
            Name = name ?? $"Test-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    public static async Task<User> CreateUserAsync(SigilDbContext context, string? displayName = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"test-{Guid.NewGuid():N}@test.com",
            Email = $"test-{Guid.NewGuid():N}@test.com",
            DisplayName = displayName ?? "Test User",
            CreatedAt = DateTime.UtcNow,
            NormalizedUserName = Guid.NewGuid().ToString("N").ToUpper(),
            NormalizedEmail = Guid.NewGuid().ToString("N").ToUpper(),
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Issue> CreateIssueAsync(SigilDbContext context, int projectId, string? title = null)
    {
        var issue = new Issue
        {
            ProjectId = projectId,
            Title = title ?? "Test Issue",
            Fingerprint = Guid.NewGuid().ToString("N")[..32],
            ExceptionType = "TestException",
            Level = Severity.Error,
            Status = IssueStatus.Open,
            Priority = Priority.Medium,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            LastChangedAt = DateTime.UtcNow,
            OccurrenceCount = 1,
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();
        return issue;
    }

    public static async Task<AlertChannel> CreateAlertChannelAsync(SigilDbContext context, string? name = null)
    {
        var channel = new AlertChannel
        {
            Name = name ?? $"Channel-{Guid.NewGuid():N}",
            Type = AlertChannelType.Webhook,
            Config = "{}",
            CreatedAt = DateTime.UtcNow,
        };
        context.AlertChannels.Add(channel);
        await context.SaveChangesAsync();
        return channel;
    }

    public static async Task<CapturedEvent> CreateEventAsync(SigilDbContext context, int projectId, int issueId,
        DateTime? timestamp = null, Severity level = Severity.Error, string? userId = null)
    {
        var evt = new CapturedEvent
        {
            EventId = Guid.NewGuid().ToString("N")[..32],
            Timestamp = timestamp ?? DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            Level = level,
            Platform = Platform.CSharp,
            IssueId = issueId,
            ProjectId = projectId,
            UserId = userId,
            RawCompressedJson = [],
        };
        context.Events.Add(evt);
        await context.SaveChangesAsync();
        return evt;
    }

    public static async Task<Release> CreateReleaseAsync(SigilDbContext context, int projectId,
        string rawName, string? semanticVersion = null, DateTime? firstSeen = null)
    {
        var release = new Release
        {
            RawName = rawName,
            ProjectId = projectId,
            SemanticVersion = semanticVersion,
            FirstSeenAt = firstSeen ?? DateTime.UtcNow,
        };
        context.Releases.Add(release);
        await context.SaveChangesAsync();
        return release;
    }

    public static async Task<StackFrame> CreateStackFrameAsync(SigilDbContext context, long eventId,
        string? function = null, bool inApp = false)
    {
        var frame = new StackFrame
        {
            EventId = eventId,
            Function = function ?? "TestMethod",
            InApp = inApp,
        };
        context.StackFrames.Add(frame);
        await context.SaveChangesAsync();
        return frame;
    }

    public static async Task<EventUser> CreateEventUserAsync(SigilDbContext context, string? identifier = null)
    {
        var user = new EventUser
        {
            UniqueIdentifier = Guid.NewGuid().ToString("N")[..32],
            Identifier = identifier,
        };
        context.EventUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<TagKey> CreateTagKeyAsync(SigilDbContext context, string key)
    {
        var tagKey = new TagKey { Key = key };
        context.TagKeys.Add(tagKey);
        await context.SaveChangesAsync();
        return tagKey;
    }

    public static async Task<AlertRule> CreateAlertRuleAsync(SigilDbContext context, int projectId, int channelId,
        bool enabled = true)
    {
        var rule = new AlertRule
        {
            Name = $"Rule-{Guid.NewGuid():N}",
            ProjectId = projectId,
            AlertChannelId = channelId,
            Trigger = AlertTrigger.NewIssue,
            Enabled = enabled,
            CooldownPeriod = TimeSpan.FromMinutes(5),
            CreatedAt = DateTime.UtcNow,
        };
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();
        return rule;
    }

    public static PlatformInfo GetPlatformInfo(Platform platform = Platform.CSharp)
    {
        return new PlatformInfoProvider().GetInfo(platform);
    }
}

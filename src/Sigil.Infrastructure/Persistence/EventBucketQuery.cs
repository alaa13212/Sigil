using Microsoft.EntityFrameworkCore;
using Sigil.Application.Models.Common;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

/// <summary>Reads stored hourly event buckets, pre-aggregated in the database when whole-day buckets suffice.</summary>
internal static class EventBucketQuery
{
    public static async Task<List<TimeSeriesPoint>> ReadAsync(IQueryable<EventBucket> query, TimeSpan interval)
    {
        var rows = SpansWholeDays(interval)
            ? await query
                .GroupBy(b => b.BucketStart.Date)
                .Select(g => new { Start = g.Key, Count = g.Sum(b => b.Count) })
                .ToListAsync()
            : await query
                .Select(b => new { Start = b.BucketStart, Count = b.Count })
                .ToListAsync();

        return [.. rows.Select(r => new TimeSeriesPoint(ToUtc(r.Start), r.Count))];
    }

    public static async Task<Dictionary<int, List<TimeSeriesPoint>>> ReadBulkAsync(IQueryable<EventBucket> query, TimeSpan interval)
    {
        if (SpansWholeDays(interval))
        {
            var daily = await query
                .GroupBy(b => new { b.IssueId, Day = b.BucketStart.Date })
                .Select(g => new { g.Key.IssueId, Start = g.Key.Day, Count = g.Sum(b => b.Count) })
                .ToListAsync();

            return daily
                .GroupBy(r => r.IssueId)
                .ToDictionary(
                    g => g.Key,
                    g => (List<TimeSeriesPoint>)[.. g.Select(r => new TimeSeriesPoint(ToUtc(r.Start), r.Count))]);
        }

        var hourly = await query
            .Select(b => new { b.IssueId, Start = b.BucketStart, Count = b.Count })
            .ToListAsync();

        return hourly
            .GroupBy(r => r.IssueId)
            .ToDictionary(
                g => g.Key,
                g => (List<TimeSeriesPoint>)[.. g.Select(r => new TimeSeriesPoint(ToUtc(r.Start), r.Count))]);
    }

    public static async Task<Dictionary<int, List<TimeSeriesPoint>>> ReadProjectBucketsAsync(IQueryable<EventBucket> query, TimeSpan interval)
    {
        if (SpansWholeDays(interval))
        {
            var daily = await query
                .GroupBy(b => new { b.Issue!.ProjectId, Day = b.BucketStart.Date })
                .Select(g => new { g.Key.ProjectId, Start = g.Key.Day, Count = g.Sum(b => b.Count) })
                .ToListAsync();

            return daily
                .GroupBy(r => r.ProjectId)
                .ToDictionary(
                    g => g.Key,
                    g => (List<TimeSeriesPoint>)[.. g.Select(r => new TimeSeriesPoint(ToUtc(r.Start), r.Count))]);
        }

        var hourly = await query
            .Select(b => new { b.Issue!.ProjectId, Start = b.BucketStart, Count = b.Count })
            .ToListAsync();

        return hourly
            .GroupBy(r => r.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => (List<TimeSeriesPoint>)[.. g.Select(r => new TimeSeriesPoint(ToUtc(r.Start), r.Count))]);
    }

    private static bool SpansWholeDays(TimeSpan interval) =>
        interval.Ticks % TimeSpan.FromDays(1).Ticks == 0;

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}

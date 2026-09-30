using System.Collections.Specialized;
using System.Globalization;
using System.Web;
using Sigil.Application.Models.Common;

namespace Sigil.Application.Models.Shared;

/// <summary>Writes a chart's range, bucket width and style into a page query string.</summary>
public static class ChartQueryState
{
    /// <summary>Query key holding the window length in days.</summary>
    public const string RangeKey = "range";

    /// <summary>Query key holding the bucket width.</summary>
    public const string BucketsKey = "buckets";

    /// <summary>Query key holding the render style.</summary>
    public const string ChartKey = "chart";

    /// <summary>
    /// Returns <paramref name="uri"/> with <paramref name="view"/> in its query string. Parameters the
    /// chart does not own are kept, and a selection equal to the default is left out of the URL.
    /// </summary>
    public static Uri Apply(Uri uri, TimeSeriesChartView view)
    {
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);

        Set(query, RangeKey, view.RangeDays.ToString(CultureInfo.InvariantCulture));
        Set(query, BucketsKey, view.Granularity == TimeSeriesGranularity.Auto
            ? null
            : view.Granularity.ToString());
        Set(query, ChartKey, view.Mode == TimeSeriesChartMode.Bar ? null : view.Mode.ToString());

        var rendered = query.ToString() ?? string.Empty;
        builder.Query = rendered.Length == 0 ? string.Empty : "?" + rendered;
        return builder.Uri;
    }

    private static void Set(NameValueCollection query, string key, string? value)
    {
        if (value is null) query.Remove(key);
        else query[key] = value;
    }
}

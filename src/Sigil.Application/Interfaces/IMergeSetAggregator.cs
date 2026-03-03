namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Refreshes aggregate statistics on merge sets after digestion.</summary>
public interface IMergeSetAggregator
{
    Task RefreshAggregatesAsync(IEnumerable<int> mergeSetIds);
}

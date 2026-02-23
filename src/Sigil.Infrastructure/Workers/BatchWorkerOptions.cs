namespace Sigil.Infrastructure.Workers;

internal class BatchWorkerOptions
{
    public int BatchSize { get; init; }
    public int? Cap { get; init; }
    public TimeSpan FlushTimeout { get; init; }
}

internal class BatchWorkersConfig : Dictionary<string, BatchWorkerOptions>
{
    public BatchWorkerOptions GetOptions(string key)
    {
        if (!TryGetValue(key, out var options))
        {
            throw new InvalidOperationException($"Missing BatchWorkerOptions for '{key}'");
        }

        return MergeWithDefaults(options);
    }
    
    private BatchWorkerOptions MergeWithDefaults(BatchWorkerOptions specific)
    {
        var defaults = new BatchWorkerOptions
        {
            BatchSize = 100,
            FlushTimeout = TimeSpan.FromSeconds(5)
        };
        
        return new BatchWorkerOptions
        {
            Cap = specific.Cap ?? defaults.Cap,
            BatchSize = specific.BatchSize != 0 ? specific.BatchSize : defaults.BatchSize,
            FlushTimeout = specific.FlushTimeout != TimeSpan.Zero ? specific.FlushTimeout : defaults.FlushTimeout
        };
    }
}
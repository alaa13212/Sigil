namespace Sigil.Infrastructure.Persistence;

internal class SaveSuppressionManager
{
    private int _suppressionCount;
    private readonly Lock _lock = new();

    public bool IsSuppressed
    {
        get
        {
            lock (_lock)
            {
                return _suppressionCount > 0;
            }
        }
    }

    public IDisposable SuppressSave()
    {
        return new SuppressionScope(this);
    }
    
    private void IncrementSuppression()
    {
        lock (_lock)
        {
            _suppressionCount++;
        }
    }

    private void DecrementSuppression()
    {
        lock (_lock)
        {
            if (_suppressionCount > 0)
            {
                _suppressionCount--;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _suppressionCount = 0;
        }
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly SaveSuppressionManager _manager;
        private bool _disposed;

        public SuppressionScope(SaveSuppressionManager manager)
        {
            _manager = manager;
            _manager.IncrementSuppression();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _manager.DecrementSuppression();
                _disposed = true;
            }
        }
    }
}

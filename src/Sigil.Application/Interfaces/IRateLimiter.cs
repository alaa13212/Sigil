namespace Sigil.Application.Interfaces;

public interface IRateLimiter
{
    bool TryAcquire(int projectId, int? projectLimit = null);
}

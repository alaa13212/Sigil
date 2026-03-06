using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Services;

public class SaveSuppressionManagerTests
{
    [Fact]
    public void Initially_NotSuppressed()
    {
        var manager = new SaveSuppressionManager();

        manager.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void SuppressSave_MakesSuppressed()
    {
        var manager = new SaveSuppressionManager();

        using var scope = manager.SuppressSave();

        manager.IsSuppressed.Should().BeTrue();
    }

    [Fact]
    public void SuppressSave_Dispose_RestoresState()
    {
        var manager = new SaveSuppressionManager();

        var scope = manager.SuppressSave();
        scope.Dispose();

        manager.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void SuppressSave_NestedScopes_RequiresAllDisposed()
    {
        var manager = new SaveSuppressionManager();

        var outer = manager.SuppressSave();
        var inner = manager.SuppressSave();

        inner.Dispose();
        manager.IsSuppressed.Should().BeTrue(); // outer still active

        outer.Dispose();
        manager.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_SafeNoOp()
    {
        var manager = new SaveSuppressionManager();

        var scope = manager.SuppressSave();
        scope.Dispose();
        scope.Dispose(); // should not decrement below 0

        manager.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsAllSuppressions()
    {
        var manager = new SaveSuppressionManager();
        manager.SuppressSave();
        manager.SuppressSave();

        manager.Reset();

        manager.IsSuppressed.Should().BeFalse();
    }
}

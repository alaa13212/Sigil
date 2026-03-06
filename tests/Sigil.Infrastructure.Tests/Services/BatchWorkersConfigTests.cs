using Sigil.Infrastructure.Workers;

namespace Sigil.Infrastructure.Tests.Services;

public class BatchWorkersConfigTests
{
    [Fact]
    public void GetOptions_MissingKey_Throws()
    {
        var config = new BatchWorkersConfig();

        var act = () => config.GetOptions("NonExistent");

        act.Should().Throw<InvalidOperationException>().WithMessage("*NonExistent*");
    }

    [Fact]
    public void GetOptions_ExistingKey_ReturnsOptions()
    {
        var config = new BatchWorkersConfig
        {
            ["Worker1"] = new BatchWorkerOptions { BatchSize = 50, FlushTimeout = TimeSpan.FromSeconds(10) }
        };

        var options = config.GetOptions("Worker1");

        options.BatchSize.Should().Be(50);
        options.FlushTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GetOptions_ZeroBatchSize_AppliesDefault100()
    {
        var config = new BatchWorkersConfig
        {
            ["Worker1"] = new BatchWorkerOptions { BatchSize = 0, FlushTimeout = TimeSpan.FromSeconds(3) }
        };

        var options = config.GetOptions("Worker1");

        options.BatchSize.Should().Be(100);
    }

    [Fact]
    public void GetOptions_ZeroFlushTimeout_AppliesDefault5Seconds()
    {
        var config = new BatchWorkersConfig
        {
            ["Worker1"] = new BatchWorkerOptions { BatchSize = 25, FlushTimeout = TimeSpan.Zero }
        };

        var options = config.GetOptions("Worker1");

        options.FlushTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetOptions_NullCap_RemainsNull()
    {
        var config = new BatchWorkersConfig
        {
            ["Worker1"] = new BatchWorkerOptions { BatchSize = 10, FlushTimeout = TimeSpan.FromSeconds(1), Cap = null }
        };

        var options = config.GetOptions("Worker1");

        options.Cap.Should().BeNull();
    }

    [Fact]
    public void GetOptions_ExplicitCap_Preserved()
    {
        var config = new BatchWorkersConfig
        {
            ["Worker1"] = new BatchWorkerOptions { BatchSize = 10, FlushTimeout = TimeSpan.FromSeconds(1), Cap = 500 }
        };

        var options = config.GetOptions("Worker1");

        options.Cap.Should().Be(500);
    }
}

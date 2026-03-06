using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class PasskeyChallengeStoreTests
{
    [Fact]
    public void Store_AndGet_ReturnsStoredJson()
    {
        var store = new PasskeyChallengeStore();
        store.Store("key1", """{"challenge":"abc"}""");

        var result = store.Get("key1");

        result.Should().Be("""{"challenge":"abc"}""");
    }

    [Fact]
    public void Get_RemovesEntryAfterRead()
    {
        var store = new PasskeyChallengeStore();
        store.Store("key1", "data");

        store.Get("key1");
        var second = store.Get("key1");

        second.Should().BeNull();
    }

    [Fact]
    public void Get_NonExistentKey_ReturnsNull()
    {
        var store = new PasskeyChallengeStore();

        store.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Store_OverwritesExistingKey()
    {
        var store = new PasskeyChallengeStore();
        store.Store("key1", "first");
        store.Store("key1", "second");

        store.Get("key1").Should().Be("second");
    }

    [Fact]
    public void Get_MultipleKeys_Independent()
    {
        var store = new PasskeyChallengeStore();
        store.Store("a", "data-a");
        store.Store("b", "data-b");

        store.Get("a").Should().Be("data-a");
        store.Get("b").Should().Be("data-b");
    }
}

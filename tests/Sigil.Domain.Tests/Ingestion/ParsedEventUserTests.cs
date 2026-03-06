using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Tests.Ingestion;

public class ParsedEventUserTests
{
    [Fact]
    public void Merge_User1WinsWhenBothHaveValue()
    {
        var user1 = new ParsedEventUser { Id = "id1", Email = "a@b.com" };
        var user2 = new ParsedEventUser { Id = "id2", Email = "x@y.com" };

        var result = ParsedEventUser.Merge(user1, user2);

        result.Id.Should().Be("id1");
        result.Email.Should().Be("a@b.com");
    }

    [Fact]
    public void Merge_User2FillsBlanks()
    {
        var user1 = new ParsedEventUser { Id = "id1" };
        var user2 = new ParsedEventUser { Email = "x@y.com", Username = "user2", IpAddress = "1.2.3.4" };

        var result = ParsedEventUser.Merge(user1, user2);

        result.Id.Should().Be("id1");
        result.Email.Should().Be("x@y.com");
        result.Username.Should().Be("user2");
        result.IpAddress.Should().Be("1.2.3.4");
    }

    [Fact]
    public void Merge_DataMerge_NoOverwrite()
    {
        var user1 = new ParsedEventUser { Data = new Dictionary<string, string> { ["key1"] = "val1" } };
        var user2 = new ParsedEventUser { Data = new Dictionary<string, string> { ["key1"] = "other", ["key2"] = "val2" } };

        var result = ParsedEventUser.Merge(user1, user2);

        result.Data.Should().ContainKey("key1").WhoseValue.Should().Be("val1"); // not overwritten
        result.Data.Should().ContainKey("key2").WhoseValue.Should().Be("val2");
    }

    [Fact]
    public void Merge_User2DataNull_User1DataUnchanged()
    {
        var user1 = new ParsedEventUser { Data = new Dictionary<string, string> { ["k"] = "v" } };
        var user2 = new ParsedEventUser { Data = null };

        var result = ParsedEventUser.Merge(user1, user2);

        result.Data.Should().ContainKey("k");
    }

    [Fact]
    public void Merge_User1DataNull_User2DataUsed()
    {
        var user1 = new ParsedEventUser { Data = null };
        var user2 = new ParsedEventUser { Data = new Dictionary<string, string> { ["k"] = "v" } };

        var result = ParsedEventUser.Merge(user1, user2);

        result.Data.Should().ContainKey("k").WhoseValue.Should().Be("v");
    }

    [Fact]
    public void Merge_ReturnsSameReferenceAsUser1()
    {
        var user1 = new ParsedEventUser();
        var user2 = new ParsedEventUser();

        var result = ParsedEventUser.Merge(user1, user2);

        result.Should().BeSameAs(user1);
    }
}

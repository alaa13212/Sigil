using Sigil.Application.Models.Auth;

namespace Sigil.Application.Tests.Models;

public class AuthResultTests
{
    private static UserInfo AnyUser() =>
        new(Guid.NewGuid(), "user@test.com", null, DateTime.UtcNow, null, []);

    [Fact]
    public void AuthResult_Success_SetsSucceededTrue()
    {
        var result = AuthResult.Success(AnyUser());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void AuthResult_Failure_SetsSucceededFalse()
    {
        var result = AuthResult.Failure("error");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void AuthResult_Failure_EnumerableOverload_SetsSucceededFalse()
    {
        var result = AuthResult.Failure(new[] { "e1", "e2" }.AsEnumerable());
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(["e1", "e2"]);
    }
}

public class InviteResultTests
{
    [Fact]
    public void InviteResult_Success_SetsSucceededTrue()
    {
        var result = InviteResult.Success(Guid.NewGuid(), "a@b.com", "https://activate");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void InviteResult_Failure_SetsSucceededFalse()
    {
        var result = InviteResult.Failure("already exists");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void InviteResult_Failure_EnumerableOverload_SetsSucceededFalse()
    {
        IEnumerable<string> errors = ["e1", "e2"];
        var result = InviteResult.Failure(errors);
        result.Succeeded.Should().BeFalse();
    }
}

public class SetupResultTests
{
    private static UserInfo AnyUser() =>
        new(Guid.NewGuid(), "admin@test.com", null, DateTime.UtcNow, null, []);

    [Fact]
    public void SetupResult_Success_SetsSucceededTrue()
    {
        var result = SetupResult.Success(AnyUser(), "api-key-123", 1);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void SetupResult_Failure_SetsSucceededFalse()
    {
        var result = SetupResult.Failure("setup already done");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void SetupResult_Failure_EnumerableOverload_SetsSucceededFalse()
    {
        IEnumerable<string> errors = ["e1", "e2"];
        var result = SetupResult.Failure(errors);
        result.Succeeded.Should().BeFalse();
    }
}

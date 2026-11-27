using Sigil.Application.Input;

namespace Sigil.Application.Tests.Input;

public class ProjectPathTests
{
    [Fact]
    public void AProjectPath_YieldsItsProjectId()
    {
        ProjectPath.ProjectIdFrom("/projects/42/issues").Should().Be(42);
    }

    [Fact]
    public void AProjectPathWithATrailingSlash_YieldsItsProjectId()
    {
        ProjectPath.ProjectIdFrom("/projects/42/").Should().Be(42);
    }

    [Fact]
    public void AnAbsoluteUrl_YieldsItsProjectId()
    {
        ProjectPath.ProjectIdFrom("https://sigil.example.com/projects/7/events").Should().Be(7);
    }

    [Fact]
    public void APathWithoutAProject_YieldsNothing()
    {
        ProjectPath.ProjectIdFrom("/").Should().BeNull();
        ProjectPath.ProjectIdFrom("/teams").Should().BeNull();
        ProjectPath.ProjectIdFrom("/admin/users").Should().BeNull();
    }

    [Fact]
    public void APathWithANonNumericProject_YieldsNothing()
    {
        ProjectPath.ProjectIdFrom("/projects/abc/issues").Should().BeNull();
    }

    [Fact]
    public void AnEmptyPath_YieldsNothing()
    {
        ProjectPath.ProjectIdFrom("").Should().BeNull();
        ProjectPath.ProjectIdFrom("   ").Should().BeNull();
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain.Enums;
using Sigil.Server.Auth;
using Sigil.Server.Controllers.API;

namespace Sigil.Server.Tests.ApiToken;

/// <summary>Covers what an API token may read: a project list without the ingestion keys.</summary>
public class ApiTokenProjectResponseTests
{
    [Fact]
    public async Task List_WithATokenPrincipal_ReturnsTheProjectsWithoutTheIngestionKey()
    {
        var service = Substitute.For<IProjectService>();
        service.GetProjectListAsync().Returns([
            new ProjectResponse(1, "web", Platform.CSharp, "the-ingestion-key")
        ]);

        var controller = CreateController(service);
        controller.ControllerContext = new ControllerContext { HttpContext = TokenContext() };

        var result = await controller.List();

        var projects = Assert.IsType<OkObjectResult>(result).Value as List<ProjectResponse>;
        projects.Should().NotBeNull();
        projects!.Should().ContainSingle();
        projects[0].Name.Should().Be("web");
        projects[0].ApiKey.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithASessionPrincipal_ReturnsTheIngestionKey()
    {
        var service = Substitute.For<IProjectService>();
        service.GetProjectListAsync().Returns([
            new ProjectResponse(1, "web", Platform.CSharp, "the-ingestion-key")
        ]);

        var controller = CreateController(service);
        controller.ControllerContext = new ControllerContext { HttpContext = SessionContext() };

        var result = await controller.List();

        var projects = Assert.IsType<OkObjectResult>(result).Value as List<ProjectResponse>;
        projects.Should().NotBeNull();
        projects![0].ApiKey.Should().Be("the-ingestion-key");
    }

    private static ProjectsController CreateController(IProjectService service) =>
        new(service, Substitute.For<IProjectEntityAccess>(), Substitute.For<IProjectConfigEditorService>());

    private static HttpContext TokenContext()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            ApiTokenAuthenticationHandler.SchemeName);

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static HttpContext SessionContext()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "cookie");

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}

namespace Sigil.Application.Models.Projects;

public record SetCorsOriginsRequest
{
    public string[] Origins { get; init; } = [];
}

public record ProjectCorsOriginsResponse(IReadOnlyList<string> Origins);

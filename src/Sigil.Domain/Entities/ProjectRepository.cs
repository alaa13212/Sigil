using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class ProjectRepository
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public int ProviderId { get; set; }

    [MaxLength(200)]
    public required string RepositoryOwner { get; set; }

    [MaxLength(200)]
    public required string RepositoryName { get; set; }

    [MaxLength(100)]
    public string? DefaultBranch { get; set; }

    public Project? Project { get; set; }
    public SourceCodeProvider? Provider { get; set; }
}

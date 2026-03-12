using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class SourceCodeProvider
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public ProviderType Type { get; set; }

    [MaxLength(500)]
    public required string BaseUrl { get; set; }

    [MaxLength(2000)]
    public required string EncryptedAccessToken { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public ICollection<ProjectRepository> Repositories { get; set; } = [];
}

public enum ProviderType { GitHub, GitLab, Bitbucket }

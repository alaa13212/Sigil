using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class AutoTagRule
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // Condition (evaluated by RuleEngine)
    public required string Field { get; set; }
    public FilterOperator Operator { get; set; }
    public required string Value { get; set; }

    // Tag to apply
    public required string TagKey { get; set; }
    public required string TagValue { get; set; }

    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

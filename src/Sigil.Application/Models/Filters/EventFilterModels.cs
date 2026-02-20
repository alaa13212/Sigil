using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Filters;

public record EventFilterResponse(
    int Id,
    int ProjectId,
    string Field,
    FilterOperator Operator,
    string Value,
    FilterAction Action,
    bool Enabled,
    int Priority,
    string? Description,
    DateTime CreatedAt);

public record CreateFilterRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    FilterAction Action = FilterAction.Reject,
    bool Enabled = true,
    int Priority = 0,
    string? Description = null);

public record UpdateFilterRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    FilterAction Action,
    bool Enabled,
    int Priority,
    string? Description);

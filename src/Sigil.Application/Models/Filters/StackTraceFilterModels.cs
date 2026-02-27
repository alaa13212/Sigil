using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Filters;

public record StackTraceFilterResponse(
    int Id,
    int ProjectId,
    string Field,
    FilterOperator Operator,
    string Value,
    bool Enabled,
    int Priority,
    string? Description,
    DateTime CreatedAt);

public record CreateStackTraceFilterRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    bool Enabled = true,
    int Priority = 0,
    string? Description = null);

public record UpdateStackTraceFilterRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    bool Enabled,
    int Priority,
    string? Description);

using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Alerts;

public record AlertRuleResponse(
    int Id,
    int ProjectId,
    string Name,
    AlertTrigger Trigger,
    AlertChannel Channel,
    int? ThresholdCount,
    TimeSpan? ThresholdWindow,
    Severity? MinSeverity,
    string ChannelConfig,
    TimeSpan CooldownPeriod,
    bool Enabled,
    DateTime CreatedAt);

public record CreateAlertRuleRequest(
    string Name,
    AlertTrigger Trigger,
    AlertChannel Channel,
    string ChannelConfig,
    int? ThresholdCount = null,
    TimeSpan? ThresholdWindow = null,
    Severity? MinSeverity = null,
    TimeSpan? CooldownPeriod = null,
    bool Enabled = true);

public record UpdateAlertRuleRequest(
    string Name,
    AlertTrigger Trigger,
    AlertChannel Channel,
    string ChannelConfig,
    int? ThresholdCount,
    TimeSpan? ThresholdWindow,
    Severity? MinSeverity,
    TimeSpan CooldownPeriod,
    bool Enabled);

public record AlertHistoryResponse(
    long Id,
    int AlertRuleId,
    string AlertRuleName,
    int? IssueId,
    string? IssueTitle,
    DateTime FiredAt,
    AlertDeliveryStatus Status,
    string? ErrorMessage);

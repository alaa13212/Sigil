using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Alerts;

public record AlertRuleResponse(
    int Id,
    int ProjectId,
    string Name,
    AlertTrigger Trigger,
    int AlertChannelId,
    string AlertChannelName,
    int? ThresholdCount,
    TimeSpan? ThresholdWindow,
    Severity? MinSeverity,
    TimeSpan CooldownPeriod,
    bool Enabled,
    DateTime CreatedAt);

public record CreateAlertRuleRequest(
    string Name,
    AlertTrigger Trigger,
    int AlertChannelId,
    int? ThresholdCount = null,
    TimeSpan? ThresholdWindow = null,
    Severity? MinSeverity = null,
    TimeSpan? CooldownPeriod = null,
    bool Enabled = true);

public record UpdateAlertRuleRequest(
    string Name,
    AlertTrigger Trigger,
    int AlertChannelId,
    int? ThresholdCount,
    TimeSpan? ThresholdWindow,
    Severity? MinSeverity,
    TimeSpan CooldownPeriod,
    bool Enabled);

public record AlertChannelResponse(
    int Id,
    string Name,
    AlertChannelType Type,
    string Config,
    DateTime CreatedAt);

public record CreateAlertChannelRequest(
    string Name,
    AlertChannelType Type,
    string Config);

public record UpdateAlertChannelRequest(
    string Name,
    AlertChannelType Type,
    string Config);

public record AlertHistoryResponse(
    long Id,
    int AlertRuleId,
    string AlertRuleName,
    int? IssueId,
    string? IssueTitle,
    DateTime FiredAt,
    AlertDeliveryStatus Status,
    string? ErrorMessage);

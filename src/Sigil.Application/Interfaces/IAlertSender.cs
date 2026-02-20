using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IAlertSender
{
    AlertChannel Channel { get; }
    Task<bool> SendAsync(AlertRule rule, Issue issue, string issueUrl);
}

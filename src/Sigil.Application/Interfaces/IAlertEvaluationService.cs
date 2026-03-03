using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Called from the digestion pipeline to evaluate alert rules.</summary>
public interface IAlertEvaluationService
{
    Task EvaluateNewIssueAsync(Issue issue);
    Task EvaluateRegressionAsync(Issue issue);
    Task EvaluateThresholdAsync(Issue issue);
}

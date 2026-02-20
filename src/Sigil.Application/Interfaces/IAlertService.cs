using Sigil.Application.Models;
using Sigil.Application.Models.Alerts;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IAlertService
{
    // Rule CRUD
    Task<List<AlertRuleResponse>> GetRulesForProjectAsync(int projectId);
    Task<AlertRuleResponse> CreateRuleAsync(int projectId, CreateAlertRuleRequest request);
    Task<AlertRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAlertRuleRequest request);
    Task<bool> DeleteRuleAsync(int ruleId);
    Task SendTestAlertAsync(int ruleId);

    // Alert evaluation (server-side only, called from DigestionService)
    Task EvaluateNewIssueAsync(Issue issue);
    Task EvaluateRegressionAsync(Issue issue);
    Task EvaluateThresholdAsync(Issue issue);

    // History
    Task<PagedResponse<AlertHistoryResponse>> GetAlertHistoryAsync(int projectId, int page = 1, int pageSize = 50);
}

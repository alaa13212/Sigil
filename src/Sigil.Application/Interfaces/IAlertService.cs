using Sigil.Application.Models;
using Sigil.Application.Models.Alerts;

namespace Sigil.Application.Interfaces;

public interface IAlertService
{
    // Rule CRUD
    Task<List<AlertRuleResponse>> GetRulesForProjectAsync(int projectId);
    Task<AlertRuleResponse> CreateRuleAsync(int projectId, CreateAlertRuleRequest request);
    Task<AlertRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAlertRuleRequest request);
    Task<bool> DeleteRuleAsync(int ruleId);
    Task<bool> ToggleRuleAsync(int ruleId, bool enabled);
    Task SendTestAlertAsync(int ruleId);

    // History
    Task<PagedResponse<AlertHistoryResponse>> GetAlertHistoryAsync(int projectId, int page = 1, int pageSize = 50);
}

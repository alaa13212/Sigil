namespace Sigil.Application.Authorization;

public static class SigilPermissions
{
    // Project-scoped
    public const string CanViewProject = "CanViewProject";
    public const string CanEditIssue = "CanEditIssue";         // status, assign, priority, comments
    public const string CanDeleteIssue = "CanDeleteIssue";
    public const string CanManageProject = "CanManageProject"; // settings, filters, rules, alerts
    public const string CanDeleteProject = "CanDeleteProject";

    // Team-scoped
    public const string CanViewTeam = "CanViewTeam";
    public const string CanManageTeam = "CanManageTeam";       // members, settings

    // Admin
    public const string CanAccessAdmin = "CanAccessAdmin";     // admin pages
    public const string CanInviteUsers = "CanInviteUsers";
}

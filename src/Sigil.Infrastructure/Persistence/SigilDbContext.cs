using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class SigilDbContext(DbContextOptions<SigilDbContext> options) : SaveSuppressionDbContext<SigilDbContext>(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<CapturedEvent> Events => Set<CapturedEvent>();
    public DbSet<EventTag> EventTags => Set<EventTag>();
    public DbSet<EventUser> EventUsers => Set<EventUser>();
    public DbSet<StackFrame> StackFrames => Set<StackFrame>();
    public DbSet<TagKey> TagKeys => Set<TagKey>();
    public DbSet<TagValue> TagValues => Set<TagValue>();
    public DbSet<IssueTag> IssueTags => Set<IssueTag>();
    public DbSet<IssueActivity> IssueActivities => Set<IssueActivity>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<RawEnvelope> RawEnvelopes => Set<RawEnvelope>();
    public DbSet<UserPasskey> Passkeys => Set<UserPasskey>();
    public DbSet<EventFilter> EventFilters => Set<EventFilter>();
    public DbSet<MergeSet> MergeSets => Set<MergeSet>();
    public DbSet<IssueBookmark> IssueBookmarks => Set<IssueBookmark>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertHistory> AlertHistory => Set<AlertHistory>();
    public DbSet<AutoTagRule> AutoTagRules => Set<AutoTagRule>();
    public DbSet<TextNormalizationRule> TextNormalizationRules => Set<TextNormalizationRule>();
    public DbSet<ProjectRecommendation> ProjectRecommendations => Set<ProjectRecommendation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AssemblyMarker).Assembly);
        
        // Rename Identity tables to avoid clutter
        builder.Entity<User>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        
    }
}
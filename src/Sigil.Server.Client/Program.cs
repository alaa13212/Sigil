using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Interfaces;
using Sigil.Server.Client.Auth;
using Sigil.Server.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthenticationStateProvider>());

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IAuthService, ApiAuthService>();
builder.Services.AddScoped<ITeamService, ApiTeamService>();
builder.Services.AddScoped<ISetupService, ApiSetupService>();
builder.Services.AddScoped<IProjectService, ApiProjectService>();
builder.Services.AddScoped<IRecommendationService, ApiRecommendationService>();
builder.Services.AddScoped<IIssueService, ApiIssueService>();
builder.Services.AddScoped<IEventService, ApiEventService>();
builder.Services.AddScoped<IIssueActivityService, ApiIssueActivityService>();
builder.Services.AddScoped<IDigestionMonitorService, ApiDigestionMonitorService>();
builder.Services.AddScoped<IPasskeyService, ApiPasskeyService>();
builder.Services.AddScoped<IEventFilterService, ApiEventFilterService>();
builder.Services.AddScoped<IMergeSetService, ApiMergeSetService>();
builder.Services.AddScoped<IBookmarkService, ApiBookmarkService>();
builder.Services.AddScoped<IAlertService, ApiAlertService>();
builder.Services.AddScoped<IAutoTagService, ApiAutoTagService>();
builder.Services.AddScoped<INormalizationRuleService, ApiNormalizationRuleService>();
builder.Services.AddScoped<IMessageNormalizer, MessageNormalizer>();
builder.Services.AddScoped<IReleaseHealthService, ApiReleaseHealthService>();

await builder.Build().RunAsync();

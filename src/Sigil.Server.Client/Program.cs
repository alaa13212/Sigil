using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sigil.Application.Interfaces;
using Sigil.Server.Client.Auth;
using Sigil.Server.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthenticationStateProvider>());

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IAuthService, ApiAuthService>();
builder.Services.AddScoped<ITeamService, ApiTeamService>();
builder.Services.AddScoped<ISetupService, ApiSetupService>();
builder.Services.AddScoped<IProjectService, ApiProjectService>();
builder.Services.AddScoped<IIssueService, ApiIssueService>();
builder.Services.AddScoped<IEventService, ApiEventService>();
builder.Services.AddScoped<IIssueActivityService, ApiIssueActivityService>();

await builder.Build().RunAsync();

using System.Text.Json;
using System.Text.Json.Serialization;
using Sigil.Application.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.DependencyInjection;
using Sigil.Domain.Interfaces;
using Sigil.infrastructure.DependencyInjection;
using Sigil.Server.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddRequestDecompression();
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
});

builder.Services.AddAuthorization();

// Core services
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddDomain();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseRequestDecompression();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Sigil.Server.Client._Imports).Assembly);

app.MapControllers();


// Auto-migrate database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var databaseMigrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await databaseMigrator.MigrateAsync();
}

app.Run();
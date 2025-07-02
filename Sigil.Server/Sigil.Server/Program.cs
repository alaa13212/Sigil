using System.Text.Json;
using System.Text.Json.Serialization;
using Sigil.Core.Ingestion;
using Sigil.Core.IssueGrouping;
using Sigil.Core.Parsing;
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

builder.Services.AddRequestDecompression();
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
});

builder.Services.AddScoped<IEventParser, SentryEventParser>();
builder.Services.AddScoped<IHashGenerator, HashGenerator>();
builder.Services.AddScoped<IFingerprintGenerator, FingerprintGenerator>();
builder.Services.AddScoped<IIngestionService, IngestionService>();


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

app.Run();
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["ConnectionStrings:Postgres"] =
    PostgresConnectionStringResolver.Resolve(builder.Configuration);

builder.Services.AddRazorPages();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<IManagementRealtimeNotifier, SignalRManagementRealtimeNotifier>();
builder.Services.AddScoped<ISubjectCatalogService, SubjectCatalogService>();
builder.Services.AddScoped<IChapterManagementService, ChapterManagementService>();
builder.Services.AddScoped<IDocumentManagementService, DocumentManagementService>();
builder.Services.AddScoped<IHomePageService, HomePageService>();
builder.Services.AddChatPageServices();
builder.Services.AddReporting();
builder.Services
    .AddDataProtection()
    .PersistKeysToDbContext<DataProtectionKeyDbContext>()
    .SetApplicationName("PRN222-RAG-Assistant");

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Render terminates TLS and supplies X-Forwarded-For / X-Forwarded-Proto.
// ASPNETCORE_FORWARDEDHEADERS_ENABLED=true configures the trusted proxy behavior on Render.
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

app.MapStaticAssets();
app.MapHub<ManagementHub>("/hubs/management");
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

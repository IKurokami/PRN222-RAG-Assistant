using Microsoft.AspNetCore.DataProtection;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Realtime;

var builder = WebApplication.CreateBuilder(args);

// Normalize either the existing ASP.NET Core connection string or a Render-style
// postgresql:// URL before Infrastructure builds Npgsql/EF Core services.
builder.Configuration["ConnectionStrings:Postgres"] =
    PostgresConnectionStringResolver.Resolve(builder.Configuration);

// Full Razor Pages architecture (controller-based pages migrated to Razor Pages)
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

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

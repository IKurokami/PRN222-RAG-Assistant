using Microsoft.AspNetCore.DataProtection;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Normalize either the existing ASP.NET Core connection string or a Render-style
// postgresql:// URL before Infrastructure builds Npgsql/EF Core services.
builder.Configuration["ConnectionStrings:Postgres"] =
    PostgresConnectionStringResolver.Resolve(builder.Configuration);

// MVC remains enabled for controller-based areas, while authentication,
// Chat UI, shell pages, and reporting use Razor Pages.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddInfrastructure(builder.Configuration);
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
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

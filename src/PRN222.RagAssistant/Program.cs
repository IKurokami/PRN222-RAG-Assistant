using PRN222.RagAssistant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Flow 1 and the pending Flow 2 use MVC Controllers + Views.
// Razor Pages remain enabled for authentication, shell pages, and Flow 3 reporting.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

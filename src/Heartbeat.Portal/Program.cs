using Heartbeat.Portal.Components;
using Heartbeat.Portal.Services;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("es"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("es");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var missionControlApiBaseUrl = builder.Configuration["MissionControlApi:BaseUrl"];
if (!Uri.TryCreate(missionControlApiBaseUrl, UriKind.Absolute, out var missionControlApiBaseAddress))
{
    throw new InvalidOperationException(
        "Configure MissionControlApi:BaseUrl with the absolute /api/mc/v1/ URL.");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDataProtection();
builder.Services.AddScoped<MissionControlSession>();
builder.Services.AddScoped<IMissionControlSessionStore, MissionControlSessionStore>();
builder.Services.AddScoped<MissionControlAuthentication>();
builder.Services.AddHttpClient("MissionControlTokenRefresh", client =>
    client.BaseAddress = missionControlApiBaseAddress);
builder.Services.AddScoped(services => new MissionControlTokenRefresher(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("MissionControlTokenRefresh"),
    services.GetRequiredService<MissionControlSession>(),
    services.GetRequiredService<IMissionControlSessionStore>()));
builder.Services.AddHttpClient<MissionControlClient>(client =>
    client.BaseAddress = missionControlApiBaseAddress);
builder.Services.AddScoped<AlertCounterState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
        }

        return Task.CompletedTask;
    });
    await next();
});
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseRequestLocalization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

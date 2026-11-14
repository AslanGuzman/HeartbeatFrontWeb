using Heartbeat.Portal.Components;
using Heartbeat.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

var missionControlApiBaseUrl = builder.Configuration["MissionControlApi:BaseUrl"];
if (!Uri.TryCreate(missionControlApiBaseUrl, UriKind.Absolute, out var missionControlApiBaseAddress))
{
    throw new InvalidOperationException(
        "Configure MissionControlApi:BaseUrl with the absolute /api/mc/v1/ URL.");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<MissionControlSession>();
builder.Services.AddHttpClient("MissionControlTokenRefresh", client =>
    client.BaseAddress = missionControlApiBaseAddress);
builder.Services.AddScoped(services => new MissionControlTokenRefresher(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("MissionControlTokenRefresh"),
    services.GetRequiredService<MissionControlSession>()));
builder.Services.AddHttpClient<MissionControlClient>(client =>
    client.BaseAddress = missionControlApiBaseAddress);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

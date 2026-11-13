using LwCadMapping.Web;
using LwCadMapping.Web.Components;
using LwCadMapping.Web.Services;
using LwCadMapping.Web.State;

var builder = WebApplication.CreateBuilder(args);

// CadMapping options. The ArcGIS key comes from user secrets / environment only; the
// spike's "CadMap:ArcGisApiKey" secret (same UserSecretsId) is honoured as a fallback.
builder.Services.Configure<CadMappingOptions>(builder.Configuration.GetSection(CadMappingOptions.Section));
builder.Services.PostConfigure<CadMappingOptions>(o =>
{
    if (string.IsNullOrWhiteSpace(o.ArcGisApiKey))
        o.ArcGisApiKey = builder.Configuration["CadMap:ArcGisApiKey"] ?? "";
    if (o.Extents.Count == 0)
        o.Extents["LW"] = [-71.382294, 42.601802, -71.231232, 42.669491];
    // ConnectionStrings:LwCad (not the spike's ConnectionStrings:Cad) replaces La.cfg's [DATABASE].
    o.ConnectionString = builder.Configuration.GetConnectionString("LwCad") ?? "";
});

builder.Services.AddDevExpressBlazor(options => options.SizeMode = DevExpress.Blazor.SizeMode.Small);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        // Keep a dropped session for 10 minutes so the reconnect in App.razor finds it again.
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
        o.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
    })
    .AddHubOptions(o =>
    {
        // In step with App.razor: the client's server timeout is 60 s and it pings every 15 s.
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        // The default 32 KB drops the circuit on a larger JS -> .NET message.
        o.MaximumReceiveMessageSize = 1024 * 1024;
    });

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("arcgis", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<ArcGisLocator>();
builder.Services.AddSingleton<LocationService>();

// One CAD reader for the server (read only), published to every circuit.
builder.Services.AddSingleton<CadBoard>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CadBoard>());

// One mapping window per circuit.
builder.Services.AddScoped<MappingSession>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

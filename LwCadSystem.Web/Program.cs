using LwCadSystem.Web.Components;
using LwCadSystem.Web.Services;

// The WPF switches (--demo, --cfg, --emp, --sess, --legacy, --kbrd, --optype,
// --sec) are taken out first and layered over appsettings as CadSystem:* values.
var hostArgs = CadStartup.ExtractSwitches(args, out var switches);

var builder = WebApplication.CreateBuilder(hostArgs);
builder.Configuration.AddInMemoryCollection(switches);

builder.Services.AddDevExpressBlazor(options => options.SizeMode = DevExpress.Blazor.SizeMode.Small);
builder.Services.Configure<CadSystemOptions>(builder.Configuration.GetSection(CadSystemOptions.Section));

// One CAD console - one ShellViewModel - per user circuit.
builder.Services.AddScoped<CadConsole>();

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
        o.MaximumReceiveMessageSize = 1024 * 1024;
    });

var app = builder.Build();

var cadOptions = builder.Configuration.GetSection(CadSystemOptions.Section).Get<CadSystemOptions>() ?? new CadSystemOptions();
CadStartup.Apply(cadOptions, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

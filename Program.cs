using VMPriceIncreases.Components;
using VMPriceIncreases.Models;
using VMPriceIncreases.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<PriceIncreaseOptions>(builder.Configuration.GetSection(PriceIncreaseOptions.SectionName));
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.SectionName));

builder.Services.AddSingleton<AzureCredentialProvider>();
builder.Services.AddSingleton<VmInventoryService>();
builder.Services.AddSingleton<PriceIncreaseCalculator>();

// RetailPriceService caches prices for the whole process, so register it as a singleton
// with a named HttpClient rather than the default transient typed-client lifetime.
builder.Services.AddHttpClient(nameof(RetailPriceService), client =>
{
    client.BaseAddress = new Uri("https://prices.azure.com/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return ActivatorUtilities.CreateInstance<RetailPriceService>(sp, factory.CreateClient(nameof(RetailPriceService)));
});

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

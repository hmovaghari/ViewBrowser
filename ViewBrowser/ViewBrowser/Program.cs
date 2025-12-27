using ViewBrowser.Models;
using ViewBrowser.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// تنظیمات Proxy
var proxyBaseUrl = builder.Configuration["ProxyBaseUrl"] ?? "https://mybrowser.ir";
builder.Services.AddSingleton(new ProxySettings
{
    EnableSsl = true,
    EnableCompression = true,
    EnableCaching = true,
    CacheDurationMinutes = 10,
    MaxConcurrentConnections = 100,
    EnableDnsOverHttps = true,
    RewriteHtml = true,
    RewriteCss = true,
    RewriteJavaScript = true,
    ProxyBaseUrl = proxyBaseUrl
});

// سرویس‌های سفارشی
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<IVpnService, VpnService>();
builder.Services.AddScoped<IContentProcessorService, ContentProcessorService>();
builder.Services.AddScoped<IProxyService, ProxyService>();

// HttpClient با تنظیمات پیشرفته
builder.Services.AddHttpClient("ProxyClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    UseCookies = true,
    CookieContainer = new System.Net.CookieContainer(),
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

// HtmlAgilityPack برای پردازش HTML
// نیاز به نصب پکیج: Install-Package HtmlAgilityPack

// Background Service برای پاکسازی سشن‌های منقضی
builder.Services.AddHostedService<VpnCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Background Service
public class VpnCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VpnCleanupService> _logger;

    public VpnCleanupService(IServiceProvider serviceProvider, ILogger<VpnCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var vpnService = scope.ServiceProvider.GetRequiredService<IVpnService>();
                await vpnService.CleanupExpiredSessionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up VPN sessions");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}

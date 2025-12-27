using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using ViewBrowser.Models;

namespace ViewBrowser.Services
{
    public interface IProxyService
    {
        Task<ProxyResponse> ForwardRequestAsync(ProxyRequest request, string sessionId);
        Task<ProxyResponse> GetResourceAsync(string url, string sessionId);
    }

    public class ProxyService : IProxyService
    {
        private readonly HttpClient _httpClient;
        private readonly IEncryptionService _encryptionService;
        private readonly IVpnService _vpnService;
        private readonly IContentProcessorService _contentProcessor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProxyService> _logger;
        private readonly ProxySettings _settings;

        public ProxyService(
            IHttpClientFactory httpClientFactory,
            IEncryptionService encryptionService,
            IVpnService vpnService,
            IContentProcessorService contentProcessor,
            IMemoryCache cache,
            ILogger<ProxyService> logger,
            ProxySettings settings)
        {
            _httpClient = httpClientFactory.CreateClient("ProxyClient");
            _encryptionService = encryptionService;
            _vpnService = vpnService;
            _contentProcessor = contentProcessor;
            _cache = cache;
            _logger = logger;
            _settings = settings;
        }

        public async Task<ProxyResponse> ForwardRequestAsync(ProxyRequest request, string sessionId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // اعتبارسنجی سشن VPN
                var session = await _vpnService.GetSessionAsync(sessionId);
                if (session == null || !await _vpnService.ValidateSessionAsync(sessionId))
                {
                    return new ProxyResponse
                    {
                        StatusCode = 401,
                        Error = "Invalid or expired VPN session"
                    };
                }

                // بررسی دامنه‌های مسدود شده
                if (!IsUrlAllowed(request.TargetUrl))
                {
                    return new ProxyResponse
                    {
                        StatusCode = 403,
                        Error = "Domain is blocked"
                    };
                }

                // به‌روزرسانی base URL سشن
                session.CurrentBaseUrl = GetBaseUrl(request.TargetUrl);

                // بررسی کش
                if (request.UseCache && request.Method == "GET")
                {
                    var cacheKey = _encryptionService.HashData($"{sessionId}:{request.TargetUrl}");
                    if (_cache.TryGetValue<ProxyResponse>(cacheKey, out var cachedResponse))
                    {
                        cachedResponse!.FromCache = true;
                        return cachedResponse;
                    }
                }

                // ساخت درخواست
                var httpRequest = new HttpRequestMessage(
                    new HttpMethod(request.Method),
                    request.TargetUrl);

                // افزودن هدرهای استاندارد
                SetDefaultHeaders(httpRequest);

                // افزودن هدرهای سفارشی
                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // افزودن کوکی‌ها
                if (request.Cookies != null && request.Cookies.Any())
                {
                    var cookieHeader = string.Join("; ", request.Cookies.Select(c => $"{c.Key}={c.Value}"));
                    httpRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                }
                else if (session.SessionCookies.Any())
                {
                    var cookieHeader = string.Join("; ", session.SessionCookies.Select(c => $"{c.Key}={c.Value}"));
                    httpRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                }

                // افزودن بدنه درخواست
                if (!string.IsNullOrEmpty(request.Body))
                {
                    httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
                }

                // ارسال درخواست
                var response = await _httpClient.SendAsync(httpRequest);
                
                // ذخیره کوکی‌های جدید در سشن
                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var cookie in setCookies)
                    {
                        var cookieParts = cookie.Split(';')[0].Split('=');
                        if (cookieParts.Length == 2)
                        {
                            session.SessionCookies[cookieParts[0].Trim()] = cookieParts[1].Trim();
                        }
                    }
                }

                // خواندن محتوا
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var isTextContent = IsTextContent(contentType);

                string content = string.Empty;
                byte[]? binaryContent = null;

                if (isTextContent)
                {
                    content = await response.Content.ReadAsStringAsync();
                    
                    // پردازش محتوای HTML
                    if (request.RewriteUrls && contentType.Contains("html"))
                    {
                        content = await _contentProcessor.ProcessHtmlAsync(
                            content, 
                            request.TargetUrl, 
                            _settings.ProxyBaseUrl, 
                            sessionId);
                    }
                    // پردازش CSS
                    else if (request.RewriteUrls && contentType.Contains("css"))
                    {
                        content = await _contentProcessor.ProcessCssAsync(
                            content, 
                            request.TargetUrl, 
                            _settings.ProxyBaseUrl, 
                            sessionId);
                    }
                }
                else
                {
                    binaryContent = await response.Content.ReadAsByteArrayAsync();
                }

                var proxyResponse = new ProxyResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType,
                    BinaryContent = binaryContent,
                    IsHtml = contentType.Contains("html"),
                    Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                    FromCache = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };

                // بررسی redirect
                if (response.StatusCode == HttpStatusCode.Moved || 
                    response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.Redirect)
                {
                    if (response.Headers.Location != null)
                    {
                        proxyResponse.RedirectUrl = response.Headers.Location.ToString();
                    }
                }

                // ذخیره در کش
                if (request.UseCache && request.Method == "GET" && response.IsSuccessStatusCode && isTextContent)
                {
                    var cacheKey = _encryptionService.HashData($"{sessionId}:{request.TargetUrl}");
                    _cache.Set(cacheKey, proxyResponse, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
                }

                return proxyResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding request to {Url}", request.TargetUrl);
                return new ProxyResponse
                {
                    StatusCode = 500,
                    Error = ex.Message
                };
            }
        }

        public async Task<ProxyResponse> GetResourceAsync(string url, string sessionId)
        {
            return await ForwardRequestAsync(new ProxyRequest
            {
                TargetUrl = url,
                Method = "GET",
                UseCache = true,
                RewriteUrls = true
            }, sessionId);
        }

        private void SetDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            request.Headers.TryAddWithoutValidation("DNT", "1");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        }

        private bool IsTextContent(string contentType)
        {
            var textTypes = new[] { "text/", "application/json", "application/xml", "application/javascript", 
                                   "application/x-javascript", "application/xhtml+xml" };
            return textTypes.Any(t => contentType.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsUrlAllowed(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host.ToLowerInvariant();

                if (_settings.BlockedDomains.Any(d => host.Contains(d.ToLowerInvariant())))
                    return false;

                if (_settings.AllowedDomains.Any() && 
                    !_settings.AllowedDomains.Any(d => host.Contains(d.ToLowerInvariant())))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetBaseUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}";
            }
            catch
            {
                return url;
            }
        }
    }
}
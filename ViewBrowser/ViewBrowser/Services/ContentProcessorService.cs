using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using ViewBrowser.Models;

namespace ViewBrowser.Services
{
    public interface IContentProcessorService
    {
        Task<string> ProcessHtmlAsync(string html, string baseUrl, string proxyBaseUrl, string sessionId);
        Task<string> ProcessCssAsync(string css, string baseUrl, string proxyBaseUrl, string sessionId);
        Task<string> ProcessJavaScriptAsync(string js, string baseUrl, string proxyBaseUrl, string sessionId);
        string RewriteUrl(string url, string baseUrl, string proxyBaseUrl, string sessionId);
    }

    public class ContentProcessorService : IContentProcessorService
    {
        private readonly ILogger<ContentProcessorService> _logger;

        public ContentProcessorService(ILogger<ContentProcessorService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ProcessHtmlAsync(string html, string baseUrl, string proxyBaseUrl, string sessionId)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // پردازش base tag
                var baseNode = doc.DocumentNode.SelectSingleNode("//base[@href]");
                if (baseNode != null)
                {
                    baseUrl = new Uri(new Uri(baseUrl), baseNode.GetAttributeValue("href", "")).ToString();
                    baseNode.Remove();
                }

                // بازنویسی لینک‌ها (a, link)
                RewriteAttributes(doc, "//a[@href]", "href", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//link[@href]", "href", baseUrl, proxyBaseUrl, sessionId);
                
                // بازنویسی تصاویر و منابع
                RewriteAttributes(doc, "//img[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//img[@srcset]", "srcset", baseUrl, proxyBaseUrl, sessionId, true);
                RewriteAttributes(doc, "//script[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//iframe[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//source[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//source[@srcset]", "srcset", baseUrl, proxyBaseUrl, sessionId, true);
                RewriteAttributes(doc, "//video[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                RewriteAttributes(doc, "//audio[@src]", "src", baseUrl, proxyBaseUrl, sessionId);
                
                // بازنویسی فرم‌ها
                RewriteAttributes(doc, "//form[@action]", "action", baseUrl, proxyBaseUrl, sessionId);
                
                // بازنویسی CSS inline
                var styleNodes = doc.DocumentNode.SelectNodes("//style");
                if (styleNodes != null)
                {
                    foreach (var styleNode in styleNodes)
                    {
                        var css = styleNode.InnerHtml;
                        styleNode.InnerHtml = await ProcessCssAsync(css, baseUrl, proxyBaseUrl, sessionId);
                    }
                }

                // بازنویسی style attributes
                var nodesWithStyle = doc.DocumentNode.SelectNodes("//*[@style]");
                if (nodesWithStyle != null)
                {
                    foreach (var node in nodesWithStyle)
                    {
                        var style = node.GetAttributeValue("style", "");
                        node.SetAttributeValue("style", await ProcessCssAsync(style, baseUrl, proxyBaseUrl, sessionId));
                    }
                }

                // اضافه کردن اسکریپت Proxy Helper
                var proxyScript = CreateProxyHelperScript(baseUrl, proxyBaseUrl, sessionId);
                var head = doc.DocumentNode.SelectSingleNode("//head");
                if (head != null)
                {
                    var scriptNode = HtmlNode.CreateNode($"<script>{proxyScript}</script>");
                    head.PrependChild(scriptNode);
                }

                // اضافه کردن meta tag برای جلوگیری از کش
                if (head != null)
                {
                    var metaNode = HtmlNode.CreateNode("<meta http-equiv=\"Content-Security-Policy\" content=\"upgrade-insecure-requests\">");
                    head.AppendChild(metaNode);
                }

                return doc.DocumentNode.OuterHtml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HTML");
                return html;
            }
        }

        public async Task<string> ProcessCssAsync(string css, string baseUrl, string proxyBaseUrl, string sessionId)
        {
            try
            {
                // بازنویسی url() در CSS
                var urlPattern = @"url\s*\(\s*['""]?([^'""()]+)['""]?\s*\)";
                var result = Regex.Replace(css, urlPattern, match =>
                {
                    var url = match.Groups[1].Value.Trim();
                    if (IsAbsoluteUrl(url) || url.StartsWith("data:") || url.StartsWith("//"))
                    {
                        if (url.StartsWith("//"))
                        {
                            url = "https:" + url;
                        }
                        var rewrittenUrl = RewriteUrl(url, baseUrl, proxyBaseUrl, sessionId);
                        return $"url('{rewrittenUrl}')";
                    }
                    else
                    {
                        var absoluteUrl = new Uri(new Uri(baseUrl), url).ToString();
                        var rewrittenUrl = RewriteUrl(absoluteUrl, baseUrl, proxyBaseUrl, sessionId);
                        return $"url('{rewrittenUrl}')";
                    }
                });

                // بازنویسی @import
                var importPattern = @"@import\s+['""]([^'""]+)['""]";
                result = Regex.Replace(result, importPattern, match =>
                {
                    var url = match.Groups[1].Value;
                    var absoluteUrl = new Uri(new Uri(baseUrl), url).ToString();
                    var rewrittenUrl = RewriteUrl(absoluteUrl, baseUrl, proxyBaseUrl, sessionId);
                    return $"@import '{rewrittenUrl}'";
                });

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSS");
                return css;
            }
        }

        public async Task<string> ProcessJavaScriptAsync(string js, string baseUrl, string proxyBaseUrl, string sessionId)
        {
            try
            {
                // اینجا می‌توانید پردازش پیشرفته‌تری انجام دهید
                // مثل بازنویسی XMLHttpRequest, fetch API, etc.
                return await Task.FromResult(js);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing JavaScript");
                return js;
            }
        }

        public string RewriteUrl(string url, string baseUrl, string proxyBaseUrl, string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:") || url.StartsWith("javascript:") || url.StartsWith("mailto:") || url.StartsWith("#"))
                {
                    return url;
                }

                // تبدیل URL نسبی به مطلق
                Uri absoluteUri;
                if (url.StartsWith("//"))
                {
                    absoluteUri = new Uri("https:" + url);
                }
                else if (IsAbsoluteUrl(url))
                {
                    absoluteUri = new Uri(url);
                }
                else
                {
                    absoluteUri = new Uri(new Uri(baseUrl), url);
                }

                // ساخت URL proxy شده
                var encodedUrl = HttpUtility.UrlEncode(absoluteUri.ToString());
                return $"{proxyBaseUrl}/api/proxy/resource?url={encodedUrl}&session={sessionId}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error rewriting URL: {Url}", url);
                return url;
            }
        }

        private void RewriteAttributes(HtmlDocument doc, string xpath, string attribute, string baseUrl, string proxyBaseUrl, string sessionId, bool isSrcSet = false)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                var value = node.GetAttributeValue(attribute, "");
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (isSrcSet)
                {
                    // پردازش srcset (می‌تواند چندین URL داشته باشد)
                    var srcSetParts = value.Split(',');
                    var rewrittenParts = new List<string>();
                    
                    foreach (var part in srcSetParts)
                    {
                        var trimmedPart = part.Trim();
                        var urlMatch = Regex.Match(trimmedPart, @"^([^\s]+)(.*)$");
                        if (urlMatch.Success)
                        {
                            var url = urlMatch.Groups[1].Value;
                            var descriptor = urlMatch.Groups[2].Value;
                            var rewrittenUrl = RewriteUrl(url, baseUrl, proxyBaseUrl, sessionId);
                            rewrittenParts.Add($"{rewrittenUrl}{descriptor}");
                        }
                    }
                    
                    node.SetAttributeValue(attribute, string.Join(", ", rewrittenParts));
                }
                else
                {
                    var rewrittenUrl = RewriteUrl(value, baseUrl, proxyBaseUrl, sessionId);
                    node.SetAttributeValue(attribute, rewrittenUrl);
                }
            }
        }

        private bool IsAbsoluteUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private string CreateProxyHelperScript(string baseUrl, string proxyBaseUrl, string sessionId)
        {
            return $@"
(function() {{
    const PROXY_BASE = '{proxyBaseUrl}';
    const SESSION_ID = '{sessionId}';
    const TARGET_BASE = '{baseUrl}';
    
    // Override fetch
    const originalFetch = window.fetch;
    window.fetch = function(url, options) {{
        if (typeof url === 'string' && !url.startsWith('data:') && !url.startsWith('blob:')) {{
            const absoluteUrl = new URL(url, TARGET_BASE).href;
            const proxyUrl = `${{PROXY_BASE}}/api/proxy/resource?url=${{encodeURIComponent(absoluteUrl)}}&session=${{SESSION_ID}}`;
            return originalFetch(proxyUrl, options);
        }}
        return originalFetch(url, options);
    }};
    
    // Override XMLHttpRequest
    const originalOpen = XMLHttpRequest.prototype.open;
    XMLHttpRequest.prototype.open = function(method, url, ...rest) {{
        if (typeof url === 'string' && !url.startsWith('data:') && !url.startsWith('blob:')) {{
            const absoluteUrl = new URL(url, TARGET_BASE).href;
            const proxyUrl = `${{PROXY_BASE}}/api/proxy/resource?url=${{encodeURIComponent(absoluteUrl)}}&session=${{SESSION_ID}}`;
            return originalOpen.call(this, method, proxyUrl, ...rest);
        }}
        return originalOpen.call(this, method, url, ...rest);
    }};
    
    // Override window.open
    const originalWindowOpen = window.open;
    window.open = function(url, ...rest) {{
        if (url && typeof url === 'string') {{
            const absoluteUrl = new URL(url, TARGET_BASE).href;
            const proxyUrl = `${{PROXY_BASE}}/Home/ProxyBrowser?url=${{encodeURIComponent(absoluteUrl)}}`;
            return originalWindowOpen.call(this, proxyUrl, ...rest);
        }}
        return originalWindowOpen.call(this, url, ...rest);
    }};
    
    console.log('🔒 Proxy Helper Loaded - All requests are routed through VPN');
}})();
";
        }
    }
}
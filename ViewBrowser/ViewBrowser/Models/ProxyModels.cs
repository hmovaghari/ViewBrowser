namespace ViewBrowser.Models
{
    public class ProxyRequest
    {
        public string TargetUrl { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public Dictionary<string, string>? Headers { get; set; }
        public Dictionary<string, string>? Cookies { get; set; }
        public string? Body { get; set; }
        public bool UseEncryption { get; set; } = true;
        public bool UseCache { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public bool RewriteUrls { get; set; } = true;
        public bool FollowRedirects { get; set; } = true;
    }

    public class ProxyResponse
    {
        public int StatusCode { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, string> Cookies { get; set; } = new();
        public bool FromCache { get; set; }
        public long ResponseTime { get; set; }
        public string? Error { get; set; }
        public byte[]? BinaryContent { get; set; }
        public bool IsHtml { get; set; }
        public string? RedirectUrl { get; set; }
    }

    public class VpnSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public string EncryptionKey { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string CurrentBaseUrl { get; set; } = string.Empty;
        public Dictionary<string, string> SessionCookies { get; set; } = new();
    }

    public class ProxySettings
    {
        public bool EnableSsl { get; set; } = true;
        public bool EnableCompression { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationMinutes { get; set; } = 10;
        public List<string> BlockedDomains { get; set; } = new();
        public List<string> AllowedDomains { get; set; } = new();
        public int MaxConcurrentConnections { get; set; } = 100;
        public bool EnableDnsOverHttps { get; set; } = true;
        public bool RewriteHtml { get; set; } = true;
        public bool RewriteCss { get; set; } = true;
        public bool RewriteJavaScript { get; set; } = true;
        public string ProxyBaseUrl { get; set; } = string.Empty;
    }

    public class ResourceInfo
    {
        public string Url { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    }
}
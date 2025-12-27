using Microsoft.AspNetCore.Mvc;
using ViewBrowser.Models;
using ViewBrowser.Services;

namespace ViewBrowser.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly IProxyService _proxyService;
        private readonly IVpnService _vpnService;
        private readonly ILogger<ProxyController> _logger;

        public ProxyController(
            IProxyService proxyService,
            IVpnService vpnService,
            ILogger<ProxyController> logger)
        {
            _proxyService = proxyService;
            _vpnService = vpnService;
            _logger = logger;
        }

        [HttpPost("connect")]
        public async Task<ActionResult<VpnSession>> Connect([FromBody] string userId)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var session = await _vpnService.CreateSessionAsync(userId, ipAddress);
            
            return Ok(new
            {
                sessionId = session.SessionId,
                encryptionKey = session.EncryptionKey,
                expiresAt = session.CreatedAt.AddHours(2)
            });
        }

        [HttpPost("forward")]
        public async Task<ActionResult<ProxyResponse>> Forward([FromBody] ProxyRequest request)
        {
            var sessionId = Request.Headers["X-VPN-Session"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                return Unauthorized(new { error = "VPN session required" });
            }

            var response = await _proxyService.ForwardRequestAsync(request, sessionId);

            if (response.StatusCode == 401 || response.StatusCode == 403)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpGet("resource")]
        public async Task<IActionResult> GetResource([FromQuery] string url, [FromQuery] string session)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(session))
            {
                return BadRequest(new { error = "URL and session are required" });
            }

            var response = await _proxyService.GetResourceAsync(url, session);

            if (!string.IsNullOrEmpty(response.Error))
            {
                return StatusCode(response.StatusCode, new { error = response.Error });
            }

            // برگرداندن محتوای باینری
            if (response.BinaryContent != null)
            {
                return File(response.BinaryContent, response.ContentType);
            }

            // برگرداندن محتوای متنی
            return Content(response.Content, response.ContentType);
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            var sessionId = Request.Headers["X-VPN-Session"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new { error = "Session ID required" });
            }

            await _vpnService.TerminateSessionAsync(sessionId);
            return Ok(new { message = "Disconnected successfully" });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var sessionId = Request.Headers["X-VPN-Session"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new { error = "Session ID required" });
            }

            var session = await _vpnService.GetSessionAsync(sessionId);
            
            if (session == null || !session.IsActive)
            {
                return NotFound(new { error = "Session not found or inactive" });
            }

            return Ok(new
            {
                sessionId = session.SessionId,
                isActive = session.IsActive,
                createdAt = session.CreatedAt,
                lastActivity = session.LastActivity,
                currentBaseUrl = session.CurrentBaseUrl
            });
        }
    }
}
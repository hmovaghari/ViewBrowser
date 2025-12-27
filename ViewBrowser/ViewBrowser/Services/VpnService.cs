using System.Collections.Concurrent;
using ViewBrowser.Models;

namespace ViewBrowser.Services
{
    public interface IVpnService
    {
        Task<VpnSession> CreateSessionAsync(string userId, string ipAddress);
        Task<bool> ValidateSessionAsync(string sessionId);
        Task TerminateSessionAsync(string sessionId);
        Task<VpnSession?> GetSessionAsync(string sessionId);
        Task CleanupExpiredSessionsAsync();
    }

    public class VpnService : IVpnService
    {
        private readonly ConcurrentDictionary<string, VpnSession> _sessions = new();
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<VpnService> _logger;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromHours(2);

        public VpnService(IEncryptionService encryptionService, ILogger<VpnService> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public Task<VpnSession> CreateSessionAsync(string userId, string ipAddress)
        {
            var session = new VpnSession
            {
                UserId = userId,
                IpAddress = ipAddress,
                EncryptionKey = _encryptionService.GenerateSecureKey(),
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true
            };

            _sessions.TryAdd(session.SessionId, session);
            _logger.LogInformation("VPN session created for user {UserId} from {IpAddress}", userId, ipAddress);

            return Task.FromResult(session);
        }

        public Task<bool> ValidateSessionAsync(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.IsActive && DateTime.UtcNow - session.LastActivity < _sessionTimeout)
                {
                    session.LastActivity = DateTime.UtcNow;
                    return Task.FromResult(true);
                }
                
                session.IsActive = false;
            }

            return Task.FromResult(false);
        }

        public Task TerminateSessionAsync(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.IsActive = false;
                _logger.LogInformation("VPN session terminated: {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }

        public Task<VpnSession?> GetSessionAsync(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }

        public Task CleanupExpiredSessionsAsync()
        {
            var expiredSessions = _sessions.Where(s => 
                !s.Value.IsActive || DateTime.UtcNow - s.Value.LastActivity >= _sessionTimeout)
                .Select(s => s.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            _logger.LogInformation("Cleaned up {Count} expired VPN sessions", expiredSessions.Count);
            return Task.CompletedTask;
        }
    }
}
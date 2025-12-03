using GenxAi_Solutions_V1.Services.Interfaces;

namespace GenxAi_Solutions_V1.Services
{
    //public class AuditLogger : IAuditLogger
    //{
    //    private readonly ILogger<AuditLogger> _logger;

    //    public AuditLogger(ILogger<AuditLogger> logger)
    //    {
    //        _logger = logger;
    //    }

    //    public void LogUserLogin(string username, string ipAddress, bool success, string reason = "")
    //    {
    //        var status = success ? "SUCCESS" : "FAILED";
    //        var reasonText = string.IsNullOrEmpty(reason) ? "" : $", Reason: {reason}";
    //        _logger.LogInformation("USER_LOGIN: User: {Username}, IP: {IpAddress}, Status: {Status}{ReasonText}",
    //            username, ipAddress, status, reasonText);
    //    }

    //    public void LogUserLogout(string username, string ipAddress)
    //    {
    //        _logger.LogInformation("USER_LOGOUT: User: {Username}, IP: {IpAddress}", username, ipAddress);
    //    }

    //    public void LogDataAccess(string username, string action, string entity, string entityId, string details)
    //    {
    //        _logger.LogInformation("DATA_ACCESS: User: {Username}, Action: {Action}, Entity: {Entity}, ID: {EntityId}, Details: {Details}",
    //            username, action, entity, entityId, details);
    //    }

    //    public void LogSecurityEvent(string eventType, string username, string ipAddress, string details)
    //    {
    //        _logger.LogInformation("SECURITY_EVENT: Type: {EventType}, User: {Username}, IP: {IpAddress}, Details: {Details}",
    //            eventType, username, ipAddress, details);
    //    }

    //    public void LogGeneralAudit(string action, string username, string ipAddress, string details)
    //    {
    //        _logger.LogInformation("GENERAL_AUDIT: Action: {Action}, User: {Username}, IP: {IpAddress}, Details: {Details}",
    //            action, username, ipAddress, details);
    //    }
    //}


    public class AuditLogger : IAuditLogger
    {
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(ILogger<AuditLogger> logger)
        {
            _logger = logger;
        }

        public void LogUserLogin(string username, string ipAddress, bool success, string reason = "")
        {
            var status = success ? "SUCCESS" : "FAILED";
            var reasonText = string.IsNullOrEmpty(reason) ? "" : $", Reason: {reason}";
            _logger.LogInformation("USER_LOGIN: User: {Username}, IP: {IpAddress}, Status: {Status}{ReasonText}",
                username, ipAddress, status, reasonText);
        }

        public void LogUserLogout(string username, string ipAddress)
        {
            _logger.LogInformation("USER_LOGOUT: User: {Username}, IP: {IpAddress}", username, ipAddress);
        }

        public void LogDataAccess(string username, string action, string entity, string entityId, string details)
        {
            _logger.LogInformation("DATA_ACCESS: User: {Username}, Action: {Action}, Entity: {Entity}, ID: {EntityId}, Details: {Details}",
                username, action, entity, entityId, details);
        }

        public void LogSecurityEvent(string eventType, string username, string ipAddress, string details)
        {
            _logger.LogInformation("SECURITY_EVENT: Type: {EventType}, User: {Username}, IP: {IpAddress}, Details: {Details}",
                eventType, username, ipAddress, details);
        }

        public void LogGeneralAudit(string action, string username, string ipAddress, string details)
        {
            _logger.LogInformation("GENERAL_AUDIT: Action: {Action}, User: {Username}, IP: {IpAddress}, Details: {Details}",
                action, username, ipAddress, details);
        }
    }
}

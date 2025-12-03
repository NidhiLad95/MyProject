namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IAuditLogger
    {
        void LogUserLogin(string username, string ipAddress, bool success, string reason = "");
        void LogUserLogout(string username, string ipAddress);
        void LogDataAccess(string username, string action, string entity, string entityId, string details);
        void LogSecurityEvent(string eventType, string username, string ipAddress, string details);
        void LogGeneralAudit(string action, string username, string ipAddress, string details);
    }
}


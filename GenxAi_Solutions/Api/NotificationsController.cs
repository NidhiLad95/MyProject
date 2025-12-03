using GenxAi_Solutions.Services.Interfaces;
using GenxAi_Solutions.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GenxAi_Solutions.Api
{
    [ApiController, Route("api/notifications")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationStore _store;
        private readonly ILogger<NotificationsController> _log;
        public NotificationsController(INotificationStore store, ILogger<NotificationsController> log) {
            _store = store;
            _log = log;
        }

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnread([FromQuery] long? afterId, CancellationToken ct)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var companies = CompanyClaimHelper.GetCompanyMemberships(User); // ← all companies

            var items = await _store.GetUnreadAfterIdAllAsync(userId, companies, afterId, ct);
            return Ok(items);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetUnread failed. afterId={AfterId}", afterId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to fetch notifications.");
            }
        }

        [HttpPost("read")]
        public async Task<IActionResult> MarkRead([FromBody] long[] ids, CancellationToken ct)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var companies = CompanyClaimHelper.GetCompanyMemberships(User);

            if (ids is null || ids.Length == 0) return Ok(new { updated = 0 });
            var updated = await _store.MarkReadAsync(companies, userId, ids, ct);
            return Ok(new { updated });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MarkRead failed for ids={Count}", ids?.Length ?? 0);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to mark notifications read.");
            }
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

public class SessionAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        //var session = context.HttpContext.Session;

        // 1️.Check if user is authenticated (normal cookie/claims check)
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Not authenticated" });
            return;
        }

        // 2️.Check if session values exist
        //var userId = session.GetInt32("UserId");
        //var email = session.GetString("Email");

        //if (userId == null || string.IsNullOrEmpty(email))
        //{
        //    context.Result = new UnauthorizedObjectResult(new { message = "Session expired. Please login again." });
        //}

        // 2) Pull values from claims (no session)
        string? userIdStr =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? // typical cookie/JWT
            user.FindFirstValue("uid") ??                     // custom
            user.FindFirstValue("UserId");                    // custom

        string? email =
            user.FindFirstValue(ClaimTypes.Name) ??
            user.FindFirstValue("email") ??
            user.FindFirstValue("Email");

        if (string.IsNullOrWhiteSpace(userIdStr) || string.IsNullOrWhiteSpace(email) || !int.TryParse(userIdStr, out var _))
        {
            // You could return 403 (Forbidden) if authenticated but malformed/missing claims.
            context.Result = new UnauthorizedObjectResult(new { message = "Missing or invalid claims. Please login again." });
            return;
        }
    }
}

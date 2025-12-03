using GenxAi_Solutions.Models;
using System.Security.Claims;

namespace GenxAi_Solutions.Services.Interfaces
{
    public interface IJwtTokenService
    {
        (string token, DateTime expiresUtc) GenerateAccessToken(User user, IEnumerable<Claim>? extraClaims = null);
        string GenerateRefreshToken(User user);
        ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
    }
}

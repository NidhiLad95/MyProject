using GenxAi_Solutions_V1.Models;
using System.Security.Claims;

namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IJwtTokenService
    {
        (string token, DateTime expiresUtc) GenerateAccessToken(User user, IEnumerable<Claim>? extraClaims = null);
        string GenerateRefreshToken(User user);
        ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
    }
}

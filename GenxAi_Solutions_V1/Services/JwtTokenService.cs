using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Models.Security;
using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GenxAi_Solutions_V1.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _opt;
        private readonly SymmetricSecurityKey _signingKey;
        private readonly TokenValidationParameters _refreshValidation;

        public JwtTokenService(IOptions<JwtOptions> opt)
        {
            _opt = opt.Value;
            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));

            // separate validation instance we can reuse for refresh verification
            _refreshValidation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _opt.Issuer,
                ValidAudience = _opt.Audience,
                IssuerSigningKey = _signingKey,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }

        public (string token, DateTime expiresUtc) GenerateAccessToken(User user, IEnumerable<Claim>? extraClaims = null)
        {
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_opt.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (extraClaims != null)
                claims.AddRange(extraClaims);

            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha512);

            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        // stateless refresh token = a second JWT with longer expiry and typ=refresh
        public string GenerateRefreshToken(User user)
        {
            var now = DateTime.UtcNow;
            var expires = now.AddDays(_opt.RefreshTokenDays);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("typ","refresh")
            };

            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha512);

            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(refreshToken, _refreshValidation, out var _);
                if (principal.FindFirst("typ")?.Value != "refresh") return null;
                return principal;
            }
            catch { return null; }
        }
    }

}

using System;
using System.Security.Cryptography;

namespace GenxAi_Solutions.Utils
{
    public static class TokenHelper
    {
        // Generates a Base64Url string (no + / =)
        public static string GenerateUrlSafeToken(int bytes = 32)
        {
            var buffer = new byte[bytes];
            RandomNumberGenerator.Fill(buffer);
            var base64 = Convert.ToBase64String(buffer);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}

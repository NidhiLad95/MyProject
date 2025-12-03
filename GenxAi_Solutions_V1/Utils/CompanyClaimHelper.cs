using System.Globalization;
using System.Security.Claims;

namespace GenxAi_Solutions_V1.Utils
{
    public static class CompanyClaimHelper
    {
        public static IReadOnlyList<int> GetCompanyMemberships(ClaimsPrincipal user)
        {
            // Support either "companies" (JSON/CSV) or "CompanyId" (JSON/CSV/single)
            var val = user.FindFirst("companies")?.Value ?? user.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrWhiteSpace(val))
                return Array.Empty<int>();

            val = val.Trim();

            // Fast path: JSON array like "[48,75,92]"
            if (val.Length > 1 && val[0] == '[' && val[^1] == ']')
            {
                try
                {
                    var js = System.Text.Json.JsonSerializer.Deserialize<List<int>>(val);
                    return (js is { Count: > 0 }) ? js! : Array.Empty<int>();
                }
                catch { /* fall through to CSV/single */ }
            }
            else
            {
                // Try JSON even if it doesn't start with [ — user sometimes sends "  [1,2] "
                try
                {
                    var js = System.Text.Json.JsonSerializer.Deserialize<List<int>>(val);
                    if (js is { Count: > 0 }) return js!;
                }
                catch { /* fall through */ }
            }

            // CSV or single value
            if (val.Contains(','))
            {
                var list = new List<int>();
                foreach (var part in val.Split(','))
                {
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                        list.Add(n);
                }
                return list.Count > 0 ? list : Array.Empty<int>();
            }

            // Single int
            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var one))
                return new[] { one };

            return Array.Empty<int>();
        }
    }
}

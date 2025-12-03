using GenxAi_Solutions_V1.Models;
using Microsoft.Extensions.VectorData;
using System.Text;
using System.Text.RegularExpressions;

namespace GenxAi_Solutions_V1.Utils
{
    public static class SqlSchemaRagTool
    {
        public sealed class Result
        {
            public bool HasAnySchema { get; init; }
            public bool HasRelevantSchema { get; init; }
            public int TotalHits { get; init; }
            public List<VectorSearchResult<SchemaRecord>> FilteredHits { get; init; } = new();
            public string SchemaContext { get; init; } = string.Empty;
        }

        public static async Task<Result> BuildSchemaContextAsync(
            string userMessage,
            SqlVectorStores stores,
            CancellationToken ct = default)
        {
            var hits = new List<VectorSearchResult<SchemaRecord>>();

            await foreach (var r in stores.Schemas.SearchAsync(userMessage, top: 5)//10
                               .WithCancellation(ct))
                hits.Add(r);

            if (hits.Count == 0)
                return new Result { HasAnySchema = false, HasRelevantSchema = false };

            var filtered = FilterSchemaHits(userMessage, hits);
            if (filtered.Count == 0)
                return new Result
                {
                    HasAnySchema = true,
                    HasRelevantSchema = false,
                    TotalHits = hits.Count
                };

            var context = BuildSqlSchemaContext(userMessage, filtered);

            return new Result
            {
                HasAnySchema = true,
                HasRelevantSchema = true,
                TotalHits = hits.Count,
                FilteredHits = filtered,
                SchemaContext = context
            };
        }

        private static List<VectorSearchResult<SchemaRecord>> FilterSchemaHits(
            string userMessage,
            List<VectorSearchResult<SchemaRecord>> hits)
        {
            if (hits.Count == 0) return hits;

            var ordered = hits.OrderByDescending(h => h.Score ?? 0d).ToList();
            var bestScore = ordered[0].Score ?? 0d;

            if (bestScore <= 0d)
                return ordered.Take(10).ToList();

            const double minAbs = 0.3;
            const double relCut = 0.6;
            var threshold = Math.Max(minAbs, bestScore * relCut);

            var above = ordered.Where(h => (h.Score ?? 0d) >= threshold).ToList();
            if (above.Count == 0) return new();

            var type = typeof(SchemaRecord);
            var tProp = type.GetProperty("TableName");
            var cProp = type.GetProperty("ColumnName");
            if (tProp == null && cProp == null) return above.Take(10).ToList();

            var tokens = ExtractCandidateNames(userMessage);
            var grouped = above.GroupBy(h =>
            {
                var t = tProp?.GetValue(h.Record) as string;
                return string.IsNullOrWhiteSpace(t) ? "__UNKNOWN__" : t!;
            });

            var tableScores = new List<(string, double, bool, List<VectorSearchResult<SchemaRecord>>)>();
            foreach (var g in grouped)
            {
                var rows = g.ToList();
                var maxScore = rows.Max(h => h.Score ?? 0d);
                var tName = g.Key;
                bool explicitMatch = false;

                if (tProp != null && tName != "__UNKNOWN__"
                    && tokens.Contains(tName.ToLowerInvariant()))
                    explicitMatch = true;

                if (!explicitMatch && cProp != null)
                {
                    foreach (var r in rows)
                    {
                        var cName = cProp.GetValue(r.Record) as string;
                        if (!string.IsNullOrWhiteSpace(cName) &&
                            tokens.Contains(cName.ToLowerInvariant()))
                        {
                            explicitMatch = true;
                            break;
                        }
                    }
                }

                tableScores.Add((tName, maxScore, explicitMatch, rows));
            }

            var candidates = tableScores.Any(t => t.Item3)
                ? tableScores.Where(t => t.Item3).ToList()
                : tableScores;

            var topTables = candidates.OrderByDescending(t => t.Item2).Take(5).ToList();
            var final = new List<VectorSearchResult<SchemaRecord>>();
            foreach (var t in topTables)
                final.AddRange(t.Item4.Take(10));
            return final;
        }

        private static string BuildSqlSchemaContext(
            string userMessage,
            IReadOnlyList<VectorSearchResult<SchemaRecord>> hits)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Relevant database schema:");
            sb.AppendLine("==========================");

            var type = typeof(SchemaRecord);
            var tProp = type.GetProperty("TableName");
            var cProp = type.GetProperty("ColumnName");
            var dProp = type.GetProperty("DataType");
            var tokens = ExtractCandidateNames(userMessage);

            if (tProp == null)
            {
                foreach (var h in hits.OrderByDescending(h => h.Score))
                    sb.AppendLine(h.Record.SchemaText);
                return sb.ToString();
            }

            var grouped = hits.GroupBy(h =>
            {
                var t = tProp.GetValue(h.Record) as string;
                return string.IsNullOrWhiteSpace(t) ? "__UNKNOWN__" : t!;
            });

            foreach (var g in grouped)
            {
                var tName = g.Key;
                sb.AppendLine($"\nTABLE: {tName}");
                if (cProp == null)
                {
                    foreach (var h in g) sb.AppendLine(h.Record.SchemaText);
                    continue;
                }

                var cols = g.Select(h => new
                {
                    Name = cProp.GetValue(h.Record) as string,
                    Type = dProp?.GetValue(h.Record) as string
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => c.Name!)
                .Select(gr => new { Name = gr.Key, Type = gr.First().Type })
                .ToList();

                var mentioned = cols.Where(c => tokens.Contains(c.Name.ToLowerInvariant())).ToList();
                var display = mentioned.Count > 0 ? mentioned : cols;

                foreach (var c in display.Take(25))
                {
                    if (string.IsNullOrWhiteSpace(c.Type))
                        sb.AppendLine($" - {c.Name}");
                    else
                        sb.AppendLine($" - {c.Name} ({c.Type})");
                }
            }

            return sb.ToString();
        }

        private static HashSet<string> ExtractCandidateNames(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return set;

            var cleaned = Regex.Replace(text, @"[^A-Za-z0-9_]+", " ");
            foreach (var token in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length < 2) continue;
                set.Add(t.ToLowerInvariant());
            }
            return set;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Utils;   // for PdfVectorStores
using Microsoft.Extensions.VectorData;

namespace GenxAi_Solutions_V1.Utils
{
    /// <summary>
    /// Shared helper for PDF RAG:
    /// - runs vector search on PdfVectorStores.Chapters
    /// - builds a compact PDF context string for the agent
    /// - returns the hits for metadata (pdfSources, pdfDocuments)
    /// </summary>
    public static class PdfRagTool
    {
        public sealed class Result
        {
            public bool HasAnyContext { get; init; }
            public List<VectorSearchResult<PdfChapterRecord>> Hits { get; init; } = new();
            public string PdfContext { get; init; } = string.Empty;
        }

        /// <summary>
        /// Run vector search in the PDF chapter index and build a compact context.
        /// </summary>
        public static async Task<Result> BuildPdfContextAsync(
            string userMessage,
            PdfVectorStores pdfStores,
            CancellationToken ct = default)
        {
            var hits = new List<VectorSearchResult<PdfChapterRecord>>();

            // Same search pattern as you had in QueryPdfWithAgent
            await foreach (var r in pdfStores.Chapters
                                             .SearchAsync(userMessage, top: 5)//10
                                             .WithCancellation(ct))
            {
                hits.Add(r);
            }

            if (hits.Count == 0)
            {
                return new Result
                {
                    HasAnyContext = false,
                    Hits = hits,
                    PdfContext = string.Empty
                };
            }

            // Build compact PDF context – same idea as your existing code:
            // - sort by score
            // - take top 6 chunks
            // - group by BookName
            // - within each book, take top 2 sections & truncate to ~500 chars
            var sb = new StringBuilder();
            sb.AppendLine("Available PDF document context:");
            sb.AppendLine("================================");

            var ordered = hits
                .OrderByDescending(h => h.Score ?? 0d)
                .Take(6)
                .ToList();

            foreach (var group in ordered.GroupBy(h => h.Record.BookName ?? "Unknown"))
            {
                sb.AppendLine($"Book: {group.Key}");

                foreach (var hit in group.Take(2)) // max 2 sections per book
                {
                    var text = hit.Record.Text ?? string.Empty;

                    if (text.Length > 500)
                        text = text.Substring(0, 500) + "...";

                    sb.AppendLine($"Content: {text}");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
            }

            return new Result
            {
                HasAnyContext = true,
                Hits = hits,
                PdfContext = sb.ToString()
            };
        }
    }
}

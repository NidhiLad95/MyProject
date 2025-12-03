using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Org.BouncyCastle.Utilities.Collections;

namespace GenxAi_Solutions_V1.Services
{
    public class PdfChatService (PdfVectorStores stores,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            ChatClientAgent pdfAgent,             // Pdf agent, see Program.cs
            IChatHistoryService history): IPdfChatService
    {
        private readonly PdfVectorStores _stores =stores;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator;
        private readonly ChatClientAgent _pdfAgent=pdfAgent;
        private readonly IChatHistoryService _history=history;
        public async Task<ChatResponseDto> AskAsync(
            string question,
            string conversationId,
            int topK)
        {
            // 1) repeated question check
            var fromHistory = _history.FindPreviousAnswer(conversationId, "pdf", question);
            if (fromHistory != null)
            {
                return new ChatResponseDto
                {
                    Answer = fromHistory,
                    ConversationId = conversationId,
                    FromHistory = true
                };
            }

            await _stores.Chapters.EnsureCollectionExistsAsync();

            // 2) Vector search chapters across all PDFs
            var chapterHits = new List<VectorSearchResult<PdfChapterRecord>>();
            await foreach (var r in _stores.Chapters.SearchAsync(question, top: Math.Max(3, topK)))
            {
                chapterHits.Add(r);
            }

            // 3) Get unique PDF slugs from chapter hits
            var pdfSlugs = chapterHits
                .Select(h => h.Record.PdfSlug)
                .Where(slug => !string.IsNullOrEmpty(slug))
                .Distinct()
                .ToList();

            // 4) Search chunks across all relevant PDF collections
            var chunkHits = new List<VectorSearchResult<PdfChunkRecord>>();
            var options = new SqliteCollectionOptions { EmbeddingGenerator = _embeddingGenerator };

            foreach (var slug in pdfSlugs.Take(3)) // Limit to top 3 PDFs to avoid too many searches
            {
                try
                {
                    var chunksCollection = new SqliteCollection<string, PdfChunkRecord>(
                        _stores.PdfConnectionString,
                        $"Book_{slug}",
                        options);

                    if (await chunksCollection.CollectionExistsAsync())
                    {
                        await foreach (var r in chunksCollection.SearchAsync(question, top: topK / pdfSlugs.Count))
                        {
                            chunkHits.Add(r);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with other collections
                    Console.WriteLine($"Error searching chunks for PDF {slug}: {ex.Message}");
                }
            }

            // If no specific PDF collections found, try common collections
            if (chunkHits.Count == 0)
            {
                var commonCollectionNames = new[] { "book_default", "book_common", "documents" };
                foreach (var collectionName in commonCollectionNames)
                {
                    try
                    {
                        var chunksCollection = new SqliteCollection<string, PdfChunkRecord>(
                            _stores.PdfConnectionString,
                            collectionName,
                            options);

                        if (await chunksCollection.CollectionExistsAsync())
                        {
                            await foreach (var r in chunksCollection.SearchAsync(question, top: topK))
                            {
                                chunkHits.Add(r);
                            }
                            break; // Stop after finding the first working collection
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error searching chunks in {collectionName}: {ex.Message}");
                    }
                }
            }

            // 5) Build context
            var ctx = new System.Text.StringBuilder();

            // Group chapters by PDF
            var chaptersByPdf = chapterHits.GroupBy(h => h.Record.PdfSlug);
            foreach (var pdfGroup in chaptersByPdf)
            {
                ctx.AppendLine($"=== PDF: {pdfGroup.Key} ===");
                foreach (var ch in pdfGroup.Take(2)) // Limit to top 2 chapters per PDF
                {
                    ctx.AppendLine($"[Chapter {ch.Record.ChapterIndex}] {ch.Record.Text?.Substring(0, Math.Min(200, ch.Record.Text.Length))}...");
                }
                ctx.AppendLine();
            }

            ctx.AppendLine("### Relevant Passages");
            foreach (var ck in chunkHits.Take(topK))
            {
                var sourceInfo = !string.IsNullOrEmpty(ck.Record.BookName) ? ck.Record.BookName : "Unknown PDF";
                ctx.AppendLine($"[{sourceInfo} - p.{ck.Record.Page}] {ck.Record.Text}");
                ctx.AppendLine();
            }

            var historyMessages = _history.GetHistory(conversationId, "pdf");

            var messages = PromptBuilder.BuildMessages(
                question,
                ctx.ToString(),
                historyMessages);

            var response = await _pdfAgent.RunAsync(messages);
            var answer = response.Text?.Trim() ?? "(no text)";

            _history.AddTurn(conversationId, "pdf", question, answer);

            // Build context for response
            var context = new List<object>();

            // Add chapter context
            context.AddRange(chapterHits.Select(h => new
            {
                id = h.Record.Id,
                pdf = h.Record.PdfSlug,
                chapter = h.Record.ChapterIndex,
                score = h.Score,
                preview = h.Record.Text?.Length > 100 ? h.Record.Text.Substring(0, 100) + "..." : h.Record.Text
            }));

            // Add chunk context
            context.AddRange(chunkHits.Select(h => new
            {
                id = h.Record.Id,
                pdf = h.Record.BookName,
                page = h.Record.Page,
                score = h.Score,
                preview = h.Record.Text?.Length > 100 ? h.Record.Text.Substring(0, 100) + "..." : h.Record.Text
            }));

            return new ChatResponseDto
            {
                Answer = answer,
                ConversationId = conversationId,
                Context = context,
                FromHistory = false
            };
        }


    }
}


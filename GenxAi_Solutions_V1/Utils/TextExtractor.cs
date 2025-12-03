using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Utils;
using ImageMagick;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using static Dapper.SqlMapper;

namespace GenxAi_Solutions_V1.Utils
{
    public class TextExtractor
    {
    

        /// <summary>
        /// Extracts text from a PDF file.
        /// If average characters per page >= densityThreshold -> returns PdfPig text.
        /// Otherwise runs OCR on each page.
        /// </summary>
        /// <param name="filePath">Path to PDF file</param>
        /// <param name="densityThreshold">Average chars/page threshold to classify as text-based</param>
        /// <returns>Extracted text (concatenated pages)</returns>
        public static string ExtractTextFromPdf(string filePath, int densityThreshold = 50)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("PDF not found", filePath);

            // 1) Try text extraction with PdfPig
            using var document = PdfDocument.Open(filePath);
            int totalChars = 0;
            int totalPages = document.NumberOfPages;
            var pageTexts = new string[Math.Max(totalPages, 1)];

            int pageIndex = 0;
            foreach (var page in document.GetPages())
            {
                string text = page.Text ?? string.Empty;
                totalChars += text.Length;
                pageTexts[pageIndex++] = text;
            }

            double avgCharsPerPage = totalChars / (double)Math.Max(totalPages, 1);

            if (avgCharsPerPage >= densityThreshold)
            {
                // Classified as text-based PDF
                return string.Join(Environment.NewLine, pageTexts.Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            // 2) IMAGE-based -> OCR using Magick + Tesseract
            var settings = new MagickReadSettings
            {
                Density = new Density(200, 200) // render at 200 DPI
            };

            using var images = new MagickImageCollection();
            images.Read(filePath, settings);

            // Tesseract tessdata directory (place tessdata folder in app base)
            string tessdataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data/tessdata");

            var ocrTexts = new string[images.Count];

            for (int i = 0; i < images.Count; i++)
            {
                try
                {
                    using var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.LstmOnly);
                    engine.SetVariable("user_defined_dpi", "300");

                    using var pageImage = (MagickImage)images[i].Clone();

                    // Preprocess the image for better OCR results
                    OcrHelpers.PreprocessForOcr(pageImage);

                    using var ms = new MemoryStream();
                    pageImage.Write(ms, MagickFormat.Png);
                    using var pix = Pix.LoadFromMemory(ms.ToArray());
                    using var pageOcr = engine.Process(pix, PageSegMode.Auto);

                    string text = pageOcr.GetText() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                        ocrTexts[i] = text;
                }
                catch (Exception)
                {
                    // Swallow per-page OCR exceptions; optionally log externally.
                    // Continue to next page.
                }
            }

            return string.Join(Environment.NewLine, ocrTexts.Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        /// <summary>
        /// Splits long text into large sections by words (Stage 1 splitting).
        /// </summary>
        public static IEnumerable<string> SplitIntoSections(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var section = new List<string>();

            foreach (var word in words)
            {
                section.Add(word);
                if (section.Count >= maxWords)
                {
                    yield return string.Join(' ', section);
                    section.Clear();
                }
            }

            if (section.Count > 0)
                yield return string.Join(' ', section);
        }

        /// <summary>
        /// Splits text into smaller chunks (used to create embeddings).
        /// </summary>
        public static IEnumerable<string> ChunkText(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var chunk = new List<string>();

            foreach (var word in words)
            {
                chunk.Add(word);
                if (chunk.Count >= maxWords)
                {
                    yield return string.Join(' ', chunk);
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
                yield return string.Join(' ', chunk);
        }


        public static (List<PdfChapterRecord> Chapters, List<PdfChunkRecord> Chunks) Read(string pdfPath, string bookName, int chapterWords = 4000, int chunkWords = 250)
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF not found.", pdfPath);

            var slug = Slugify(bookName);

            var allWords = new List<(int Page, string Word)>();

            using (var doc = PdfDocument.Open(pdfPath))
            {
                foreach (var page in doc.GetPages())
                {
                    var text = page.Text;
                    var words = SplitWords(text);
                    allWords.AddRange(words.Select(w => (page.Number, w)));
                }
            }

            var chapters = new List<PdfChapterRecord>();
            var chunks = new List<PdfChunkRecord>();

            int currentChapter = 0;
            int chapterCount = 0;
            int chunkCount = 0;
            var chapterSb = new StringBuilder();
            var chunkSb = new StringBuilder();
            int chunkIndex = 0;
            int startOffset = 0;
            int currentPage = 1;

            for (int i = 0; i < allWords.Count; i++)
            {
                var (page, word) = allWords[i];
                currentPage = page;

                if (chapterSb.Length > 0) chapterSb.Append(' ');
                if (chunkSb.Length > 0) chunkSb.Append(' ');

                chapterSb.Append(word);
                chunkSb.Append(word);

                chapterCount++;
                chunkCount++;

                // finalize chunk (~250 words)
                if (chunkCount >= chunkWords || i == allWords.Count - 1)
                {
                    var textChunk = chunkSb.ToString();
                    var summary = textChunk.Length <= 500 ? textChunk : textChunk[..500] + "...";

                    chunks.Add(new PdfChunkRecord
                    {
                        Id = $"{slug}_chunk_{chunkIndex}",
                        BookName = bookName,
                        PdfSlug = slug,
                        Page = currentPage,
                        ChunkIndex = chunkIndex,
                        StartOffset = startOffset,
                        EndOffset = startOffset + chunkCount,
                        Text = textChunk
                    });

                    startOffset += chunkCount;
                    chunkIndex++;
                    chunkCount = 0;
                    chunkSb.Clear();
                }

                // finalize chapter (~4000 words)
                if (chapterCount >= chapterWords || i == allWords.Count - 1)
                {
                    var textChap = chapterSb.ToString();
                    var summary = textChap.Length <= 800 ? textChap : textChap[..800] + "...";

                    chapters.Add(new PdfChapterRecord
                    {
                        Id = $"{slug}_chapter_{currentChapter}",
                        BookName = bookName,
                        PdfSlug = slug,
                        ChapterIndex = currentChapter,
                        Text = textChap
                    });

                    currentChapter++;
                    chapterCount = 0;
                    chapterSb.Clear();
                }
            }

            return (chapters, chunks);
        }

        private static IEnumerable<string> SplitWords(string text)
        {
            var sep = new[] { ' ', '\r', '\n', '\t' };
            return text.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        }
        private static string Slugify(string s)
        {
            var slug = System.Text.RegularExpressions.Regex
                .Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "_")
                .Trim('_');
            return slug;
        }

    }
}


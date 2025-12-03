using GenxAi_Solutions.Utils;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Tesseract;
using ImageMagick;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Org.BouncyCastle.Asn1.Pkcs;
using static Dapper.SqlMapper;

namespace GenxAi_Solutions.Utils
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
    }
}


using ImageMagick;

namespace GenxAi_Solutions.Utils
{

    public static class OcrHelpers
    {
        /// <summary>
        /// Preprocesses a MagickImage for OCR.
        /// - convert to grayscale
        /// - remove alpha
        /// - auto-level / increase contrast
        /// - optional deskewing for large pages
        /// - modest sharpening
        /// - optional adaptive thresholding if 'aggressive' is true
        /// </summary>
        public static void PreprocessForOcr(MagickImage img, bool aggressive = false)
        {
            if (img is null) return;

            // Convert to grayscale and remove alpha channel
            img.ColorType = ColorType.Grayscale;
            img.Alpha(AlphaOption.Off);

            // Normalize tones / improve contrast
            img.AutoLevel();

            // Deskew slightly rotated scans if image is large enough
            if (img.Width > 1000 && img.Height > 1000)
            {
                try
                {
                    img.Deskew(new Percentage(35));
                }
                catch
                {
                    // Deskew sometimes fails; ignore and continue.
                }
            }

            // Slight sharpening to enhance stroke edges
            img.AdaptiveSharpen(1, 1);

            // Optional adaptive threshold — useful for faint text but can harm figures
            if (aggressive)
            {
                img.AdaptiveThreshold(15, 15, 5);
            }
        }
    }
}

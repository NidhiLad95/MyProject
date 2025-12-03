using Microsoft.Extensions.VectorData;

namespace GenxAi_Solutions_V1.Models
{
    public static class PdfVectorDefaults
    {
        public const int EmbeddingDimensions = 1536;
    }

    // Common chapter table (4k word chunks) – common collection in PDF DB
    public sealed class PdfChapterRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreVector(Dimensions: 1536)]
        public string Embedding => Text;

        [VectorStoreData]
        public string Text { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public string BookName { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public string PdfSlug { get; set; } = string.Empty; // e.g. "design_patterns"

        [VectorStoreData]
        public int ChapterIndex { get; set; }



        //[VectorStoreData]
        //public string Summary { get; set; } = string.Empty;

        // Embedding from summary

    }

    // Per-PDF chunk table (250-word chunks) – table prefix Book_
    public sealed class PdfChunkRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreVector(Dimensions: PdfVectorDefaults.EmbeddingDimensions)]
        public string Embedding => Text;

        [VectorStoreData(IsIndexed = true)]
        public string BookName { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public string PdfSlug { get; set; } = string.Empty;

        [VectorStoreData]
        public int Page { get; set; }

        [VectorStoreData]
        public int ChunkIndex { get; set; }

        [VectorStoreData]
        public int StartOffset { get; set; }

        [VectorStoreData]
        public int EndOffset { get; set; }

        [VectorStoreData]
        public string Text { get; set; } = string.Empty;

        //[VectorStoreData]
        //public string Summary { get; set; } = string.Empty;


    }
}

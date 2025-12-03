using Microsoft.Extensions.VectorData;

namespace GenxAi_Solutions_V1.Models
{
    public class SchemaEntry
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaText { get; set; } = string.Empty; // "TABLE dbo.Orders ... Columns ..."
    }

    // Vector record stored in SQLite (SQL DB)
    public sealed class SchemaRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public string Name { get; set; } = string.Empty;

        [VectorStoreData]
        public string SchemaText { get; set; } = string.Empty;

        // Auto-embedding from SchemaText
        [VectorStoreVector(Dimensions: 1536)]
        public string Embedding => SchemaText;
    }

    public sealed class PromptRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData]
        public string Text { get; set; } = string.Empty;

        [VectorStoreVector(Dimensions: 1536)]
        public string Embedding => Text;
    }
}

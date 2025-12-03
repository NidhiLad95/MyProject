using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class SchemaEntry
    {
        public string TableName { get; }
        public string SchemaText { get; }
        public float[] Embedding { get; }

        public SchemaEntry(string tableName, string schemaText, float[] embedding)
        {
            TableName = tableName;
            SchemaText = schemaText;
            Embedding = embedding;
        }
    }

    public class PromptEntry
    {
        public string PromptName { get; }
        public string PromptText { get; }
        public float[] Embedding { get; }

        public PromptEntry(string promptName, string promptText, float[] embedding)
        {
            PromptName = promptName;
            PromptText = promptText;
            Embedding = embedding;
        }
    }

    public class PromptEntries
    {
        public string PromptId { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";     // short label
        public string Text { get; set; } = "";      // the actual prompt content
        public string? TagsCsv { get; set; }        // optional tags: "sales, finance, customer"
        public float[] Embedding { get; set; } = Array.Empty<float>();

    }
    public class ColumnEntry
    {
        public string TableName { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string ColumnType { get; set; } = "";
        public string? AliasesCsv { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    public class DocumentChunkEntry
    {
        public string DocId { get; set; } = default!;
        public int SectionIndex { get; set; }
        public int ChunkIndex { get; set; }
        public string Text { get; set; } = default!;
        public float[] Embedding { get; set; } = default!;
    }
}

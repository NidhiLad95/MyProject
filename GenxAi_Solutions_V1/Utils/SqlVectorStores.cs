using GenxAi_Solutions_V1.Models;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace GenxAi_Solutions_V1.Utils
{
    public sealed class SqlVectorStores
    {
        public SqliteCollection<string, SchemaRecord> Schemas { get; }
        public SqliteCollection<string, PromptRecord> Prompts { get; }

        public SqlVectorStores(
            SqliteCollection<string, SchemaRecord> schemas,
            SqliteCollection<string, PromptRecord> prompts)
           
        {
            Schemas = schemas;
            Prompts = prompts;
           
        }
    }

    public sealed class PdfVectorStores
    {
        public string PdfConnectionString { get; }
        public SqliteCollection<string, PdfChapterRecord> Chapters { get; }
        public SqliteCollection<string, PromptRecord> Prompts { get; }
        public PdfVectorStores(string connString,
            SqliteCollection<string, PdfChapterRecord> chapters,
            SqliteCollection<string, PromptRecord> prompts)
        {
            PdfConnectionString = connString;
            Chapters = chapters;
            Prompts = prompts;
        }
    }
}

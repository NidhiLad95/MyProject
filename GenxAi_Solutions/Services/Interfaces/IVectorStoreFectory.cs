using GenxAi_Solutions.Utils;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace GenxAi_Solutions.Services.Interfaces
{
    public interface IVectorStoreFactory
    {
        SQLiteVectorStore Create(string dbName);
        SqliteVectorStore PdfCreate(string dbName);
    }
}

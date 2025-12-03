using GenxAi_Solutions_V1.Utils;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IVectorStoreFactory
    {
        //SQLiteVectorStore Create(string dbName);        
        //SqliteVectorStore PdfCreate(string dbName);

        SqlVectorStores Create_New(string dbName);
        PdfVectorStores PdfCreate_New(string dbName);
    }
}

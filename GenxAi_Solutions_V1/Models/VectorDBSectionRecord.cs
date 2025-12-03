
using Microsoft.Extensions.VectorData;

namespace GenxAi_Solutions_V1.Models
{
    public class VectorDBSectionRecord
    {
        [VectorStoreKey] public string Key { get; set; } = string.Empty;
        [VectorStoreVector(1536)] public ReadOnlyMemory<float>? Embedding { get; set; }
        [VectorStoreData] public string Source { get; set; } = string.Empty;
    }
}

namespace GenxAi_Solutions.Dtos
{
    public record UploadResponse(
       string FileName,
       string CollectionName,
       int SectionsStored
    );
}
